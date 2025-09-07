// Install tools.
#tool dotnet:?package=GitVersion.Tool&version=6.4.0
#tool dotnet:?package=coveralls.net&version=4.0.1
#tool nuget:?package=GitReleaseManager&version=0.20.0
#tool nuget:?package=ReportGenerator&version=5.4.12
#tool nuget:?package=xunit.runner.console&version=2.9.3
#tool nuget:?package=CodecovUploader&version=0.8.0

// Install addins.
#addin nuget:?package=Cake.Coveralls&version=4.0.0
#addin nuget:?package=Cake.Git&version=5.0.1
#addin nuget:?package=Cake.Codecov&version=3.0.0


///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

if (IsRunningOnUnix()) target = "Run-Unit-Tests";


///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var libraryName = "HttpMultipartParser";
var gitHubRepo = "Http-Multipart-Data-Parser";

var nuGetApiUrl = Argument<string>("NUGET_API_URL", EnvironmentVariable("NUGET_API_URL"));
var nuGetApiKey = Argument<string>("NUGET_API_KEY", EnvironmentVariable("NUGET_API_KEY"));

var gitHubToken = Argument<string>("GITHUB_TOKEN", EnvironmentVariable("GITHUB_TOKEN"));
var gitHubUserName = Argument<string>("GITHUB_USERNAME", EnvironmentVariable("GITHUB_USERNAME"));
var gitHubPassword = Argument<string>("GITHUB_PASSWORD", EnvironmentVariable("GITHUB_PASSWORD"));
var gitHubRepoOwner = Argument<string>("GITHUB_REPOOWNER", EnvironmentVariable("GITHUB_REPOOWNER") ?? gitHubUserName);

var coverallsToken = Argument<string>("COVERALLS_REPO_TOKEN", EnvironmentVariable("COVERALLS_REPO_TOKEN"));
var codecovToken = Argument<string>("CODECOV_TOKEN", EnvironmentVariable("CODECOV_TOKEN"));

var sourceFolder = "./Source/";
var outputDir = "./artifacts/";
var codeCoverageDir = $"{outputDir}CodeCoverage/";
var benchmarkDir = $"{outputDir}Benchmark/";
var coverageFile = $"{codeCoverageDir}coverage.xml";

var solutionFile = $"{sourceFolder}{libraryName}.sln";
var sourceProject = $"{sourceFolder}{libraryName}/{libraryName}.csproj";
var integrationTestsProject = $"{sourceFolder}{libraryName}.IntegrationTests/{libraryName}.IntegrationTests.csproj";
var unitTestsProject = $"{sourceFolder}{libraryName}.UnitTests/{libraryName}.UnitTests.csproj";
var benchmarkProject = $"{sourceFolder}{libraryName}.Benchmark/{libraryName}.Benchmark.csproj";

var buildBranch = Context.GetBuildBranch();
var repoName = Context.GetRepoName();

var versionInfo = (GitVersion)null; // Will be calculated in SETUP
var milestone = string.Empty; // Will be calculated in SETUP

var cakeVersion = typeof(ICakeContext).Assembly.GetName().Version.ToString();
var isLocalBuild = BuildSystem.IsLocalBuild;
var isMainBranch = StringComparer.OrdinalIgnoreCase.Equals("main", buildBranch);
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals($"{gitHubRepoOwner}/{gitHubRepo}", repoName);
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);
var isIntegrationTestsProjectPresent = FileExists(integrationTestsProject);
var isUnitTestsProjectPresent = FileExists(unitTestsProject);
var isBenchmarkProjectPresent = FileExists(benchmarkProject);
var removeIntegrationTests = isIntegrationTestsProjectPresent && !isLocalBuild;
var removeBenchmarks = isBenchmarkProjectPresent && !isLocalBuild;

var publishingError = false;

// A single framework is sufficient when calculating code coverage.
const string DESIRED_FRAMEWORK_FOR_CODE_COVERAGE = "net9.0";

// The terminal logger introduced but turned off by default in .NET8 and turned on by default in .NET9
// doesn't work right on Linux and causes a lot of noise in the build log on Ubuntu in AppVeyor.
// As of March 2025, the terminal logger doesn't seem to work right on Windows in AppVeyor either.
// Therefore I am enabling it when building on my machine and turning it off in any other environment.
var terminalLogger = (isLocalBuild && IsRunningOnWindows()) ? "on" : "off";


///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
	if (!isLocalBuild && context.Log.Verbosity != Verbosity.Diagnostic)
	{
		Information("Increasing verbosity to diagnostic.");
		context.Log.Verbosity = Verbosity.Diagnostic;
	}

	Information("Calculating version info...");
	versionInfo = GitVersion(new GitVersionSettings() { OutputType = GitVersionOutput.Json });
	milestone = versionInfo.MajorMinorPatch;

	Information("Building version {0} of {1} ({2}, {3}) using version {4} of Cake",
		versionInfo.FullSemVer,
		libraryName,
		configuration,
		target,
		cakeVersion
	);

	Information("Variables:\r\n\tLocalBuild: {0}\r\n\tIsMainBranch: {1}\r\n\tIsMainRepo: {2}\r\n\tIsPullRequest: {3}\r\n\tIsTagged: {4}",
		isLocalBuild,
		isMainBranch,
		isMainRepo,
		isPullRequest,
		isTagged
	);

	Information("Nuget Info:\r\n\tApi Url: {0}\r\n\tApi Key: {1}",
		nuGetApiUrl,
		string.IsNullOrEmpty(nuGetApiKey) ? "[NULL]" : new string('*', nuGetApiKey.Length)
	);

	if (!string.IsNullOrEmpty(gitHubToken))
	{
		Information("GitHub Info:\r\n\tRepo: {0}\r\n\tUserName: {1}\r\n\tToken: {2}",
			$"{gitHubRepoOwner}/{gitHubRepo}",
			gitHubUserName,
			new string('*', gitHubToken.Length)
		);
	}
	else
	{
		Information("GitHub Info:\r\n\tRepo: {0}\r\n\tUserName: {1}\r\n\tPassword: {2}",
			$"{gitHubRepoOwner}/{gitHubRepo}",
			gitHubUserName,
			string.IsNullOrEmpty(gitHubPassword) ? "[NULL]" : new string('*', gitHubPassword.Length)
		);
	}

	// Integration tests are intended to be used for debugging purposes and not intended to be executed in CI environment.
	if (removeIntegrationTests)
	{
		Information("");
		Information("Removing integration tests");
		DotNetTool(solutionFile, "sln", $"remove {integrationTestsProject.TrimStart(sourceFolder, StringComparison.OrdinalIgnoreCase)}");
	}

	// Similarly, benchmarks are not intended to be executed in CI environment.
	if (removeBenchmarks)
	{
		Information("");
		Information("Removing benchmark project");
		DotNetTool(solutionFile, "sln", $"remove {benchmarkProject.TrimStart(sourceFolder, StringComparison.OrdinalIgnoreCase)}");
	}
});

Teardown(context =>
{
	if (removeIntegrationTests || removeBenchmarks)
	{
		Information("Restoring the solution file which was modified during build script setup");
		GitCheckout(".", new FilePath[] { solutionFile });
		Information("  Restored {0}", solutionFile.ToString());
		Information("");
	}

	// Executed AFTER the last task.
	Information("Finished running tasks.");
});


///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("AppVeyor-Build_Number")
	.WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
	.Does(() =>
{
	GitVersion(new GitVersionSettings()
	{
		UpdateAssemblyInfo = false,
		OutputType = GitVersionOutput.BuildServer
	});
});

Task("Clean")
	.IsDependentOn("AppVeyor-Build_Number")
	.Does(() =>
{
	// Clean solution directories.
	Information("Cleaning {0}", sourceFolder);
	CleanDirectories($"{sourceFolder}*/bin/{configuration}");
	CleanDirectories($"{sourceFolder}*/obj/{configuration}");

	// Clean previous artifacts
	Information("Cleaning {0}", outputDir);
	if (DirectoryExists(outputDir)) CleanDirectories(MakeAbsolute(Directory(outputDir)).FullPath);
	else CreateDirectory(outputDir);

	// Create folder for code coverage report
	CreateDirectory(codeCoverageDir);
});

Task("Restore-NuGet-Packages")
	.IsDependentOn("Clean")
	.Does(() =>
{
	DotNetRestore("./Source/", new DotNetRestoreSettings
	{
		Sources = new [] {
			"https://api.nuget.org/v3/index.json",
		},
		ArgumentCustomization = args => args
			.Append($"-tl:{terminalLogger}")
	});
});

Task("Build")
	.IsDependentOn("Restore-NuGet-Packages")
	.Does(() =>
{
	DotNetBuild(solutionFile, new DotNetBuildSettings
	{
		Configuration = configuration,
		NoRestore = true,
		MSBuildSettings = new DotNetMSBuildSettings
		{
			Version = versionInfo.SemVer,
			AssemblyVersion = versionInfo.MajorMinorPatch,
			FileVersion = versionInfo.MajorMinorPatch,
			InformationalVersion = versionInfo.InformationalVersion,
			ContinuousIntegrationBuild = true
		},
		ArgumentCustomization = args => args
			.Append($"-tl:{terminalLogger}")
	});
});

Task("Run-Unit-Tests")
	.WithCriteria(() => isUnitTestsProjectPresent)
	.IsDependentOn("Build")
	.Does(() =>
{
	DotNetTest(unitTestsProject, new DotNetTestSettings
	{
		NoBuild = true,
		NoRestore = true,
		Configuration = configuration,
		ArgumentCustomization = args => args
			.Append($"-tl:{terminalLogger}")
	});
});

Task("Run-Code-Coverage")
	.WithCriteria(() => isUnitTestsProjectPresent)
	.IsDependentOn("Build")
	.Does(() =>
{
	var testSettings = new DotNetTestSettings
	{
		NoBuild = true,
		NoRestore = true,
		Configuration = configuration,
		Framework = DESIRED_FRAMEWORK_FOR_CODE_COVERAGE,

		// The following assumes that coverlet.msbuild has been added to the unit testing project
		ArgumentCustomization = args => args
			.Append($"-tl:{terminalLogger}")
			.Append("--")
			.Append("--coverage")
			.Append("--coverage-output-format xml")
			.Append($"--coverage-output {MakeAbsolute(new FilePath(coverageFile))}")
			.Append($"--coverage-settings {MakeAbsolute(new FilePath("CodeCoverage.runsettings"))}")
    };

    DotNetTest(unitTestsProject, testSettings);
});

Task("Upload-Coverage-Result-Coveralls")
	.IsDependentOn("Run-Code-Coverage")
    .WithCriteria(() => FileExists(coverageFile))
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.Does(() =>
{
	if(string.IsNullOrEmpty(coverallsToken)) throw new InvalidOperationException("Could not resolve Coveralls token.");

	CoverallsNet(new FilePath(coverageFile), CoverallsNetReportType.OpenCover, new CoverallsNetSettings()
	{
		RepoToken = coverallsToken,
		UseRelativePaths = true
	});
}).OnError (exception =>
{
    Information(exception.Message);
    Information($"Failed to upload coverage result to Coveralls, but continuing with next Task...");
    publishingError = true;
});

Task("Upload-Coverage-Result-Codecov")
	.IsDependentOn("Run-Code-Coverage")
    .WithCriteria(() => FileExists(coverageFile))
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.Does(() =>
{
	if(string.IsNullOrEmpty(codecovToken)) throw new InvalidOperationException("Could not resolve CodeCov token.");

	Codecov(new CodecovSettings
    {
        Files = new[] { coverageFile },
        Token = codecovToken
    });
}).OnError (exception =>
{
    Information(exception.Message);
    Information($"Failed to upload coverage result to Codecov, but continuing with next Task...");
    publishingError = true;
});

Task("Generate-Code-Coverage-Report")
	.IsDependentOn("Run-Code-Coverage")
	.Does(() =>
{
	ReportGenerator(
		new FilePath(coverageFile),
		codeCoverageDir,
		new ReportGeneratorSettings() {
			ClassFilters = new[] { "+*" }
		}
	);
});

Task("Create-NuGet-Package")
	.IsDependentOn("Build")
	.Does(() =>
{
	var releaseNotesUrl = @$"https://github.com/{gitHubRepoOwner}/{gitHubRepo}/releases/tag/{milestone}";

	var settings = new DotNetPackSettings
	{
		Configuration = configuration,
		IncludeSource = false,
		IncludeSymbols = true,
		NoBuild = true,
		NoRestore = true,
		NoDependencies = true,
		OutputDirectory = outputDir,
		SymbolPackageFormat = "snupkg",
		MSBuildSettings = new DotNetMSBuildSettings
		{
			PackageReleaseNotes = releaseNotesUrl,
			PackageVersion = versionInfo.FullSemVer.Replace('+', '-')
		},
		ArgumentCustomization = args => args
			.Append($"-tl:{terminalLogger}")
	};

	DotNetPack(sourceProject, settings);
});

Task("Upload-AppVeyor-Artifacts")
	.WithCriteria(() => AppVeyor.IsRunningOnAppVeyor)
	.Does(() =>
{
	var allFiles = GetFiles($"{outputDir}*.*") +
		GetFiles($"{benchmarkDir}results/*.*") +
		GetFiles($"{codeCoverageDir}*.*");

	foreach (var file in allFiles)
	{
		AppVeyor.UploadArtifact(file.FullPath);
	}
});

Task("Publish-NuGet")
	.IsDependentOn("Create-NuGet-Package")
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.WithCriteria(() => isMainBranch)
	.WithCriteria(() => isTagged)
	.Does(() =>
{
	if(string.IsNullOrEmpty(nuGetApiKey)) throw new InvalidOperationException("Could not resolve NuGet API key.");
	if(string.IsNullOrEmpty(nuGetApiUrl)) throw new InvalidOperationException("Could not resolve NuGet API url.");

	var settings = new DotNetNuGetPushSettings
	{
    	Source = nuGetApiUrl,
	    ApiKey = nuGetApiKey
	};

	foreach(var package in GetFiles(outputDir + "*.nupkg"))
	{
		DotNetNuGetPush(package, settings);
	}
});

Task("Create-Release-Notes")
	.Does(() =>
{
	if (string.IsNullOrEmpty(gitHubToken))
	{
		throw new InvalidOperationException("GitHub token was not provided.");
	}

	GitReleaseManagerCreate(gitHubToken, gitHubRepoOwner, gitHubRepo, new GitReleaseManagerCreateSettings
	{
		Name            = milestone,
		Milestone       = milestone,
		Prerelease      = false,
		TargetCommitish = "main",
		Verbose         = true
	});
});

Task("Publish-GitHub-Release")
	.WithCriteria(() => !isLocalBuild)
	.WithCriteria(() => !isPullRequest)
	.WithCriteria(() => isMainRepo)
	.WithCriteria(() => isMainBranch)
	.WithCriteria(() => isTagged)
	.Does(() =>
{
	if (string.IsNullOrEmpty(gitHubToken))
	{
		throw new InvalidOperationException("GitHub token was not provided.");
	}

	GitReleaseManagerClose(gitHubToken, gitHubRepoOwner, gitHubRepo, milestone, new GitReleaseManagerCloseMilestoneSettings
	{
		Verbose = true
	});
});

Task("Generate-Benchmark-Report")
	.IsDependentOn("Build")
	.WithCriteria(isBenchmarkProjectPresent)
	.Does(() =>
{
    var publishDirectory = $"{benchmarkDir}Publish/";
    var publishedAppLocation = MakeAbsolute(File($"{publishDirectory}{libraryName}.Benchmark.exe")).FullPath;
    var artifactsLocation = MakeAbsolute(File(benchmarkDir)).FullPath;

    DotNetPublish(benchmarkProject, new DotNetPublishSettings
    {
        Configuration = configuration,
		NoRestore = true,
        NoBuild = true,
        OutputDirectory = publishDirectory,
		ArgumentCustomization = args => args
			.Append($"-tl:{terminalLogger}")
    });

	using (DiagnosticVerbosity())
    {
        var processResult = StartProcess(
            publishedAppLocation,
            new ProcessSettings()
            {
                Arguments = $"-f * --artifacts={artifactsLocation}"
            });
        if (processResult != 0)
        {
            throw new Exception($"dotnet-benchmark.exe did not complete successfully. Result code: {processResult}");
        }
    }
});


///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Coverage")
	.IsDependentOn("Generate-Code-Coverage-Report")
	.Does(() =>
{
	StartProcess("cmd", $"/c start {codeCoverageDir}index.htm");
});

Task("Benchmark")
	.IsDependentOn("Generate-Benchmark-Report")
	.WithCriteria(isBenchmarkProjectPresent)
	.Does(() =>
{
    var htmlReports = GetFiles($"{benchmarkDir}results/*-report.html", new GlobberSettings { IsCaseSensitive = false });
	foreach (var htmlReport in htmlReports)
	{
		StartProcess("cmd", $"/c start {htmlReport}");
	}
});

Task("ReleaseNotes")
	.IsDependentOn("Create-Release-Notes");

Task("AppVeyor")
	.IsDependentOn("Run-Code-Coverage")
	.IsDependentOn("Upload-Coverage-Result-Coveralls")
	.IsDependentOn("Upload-Coverage-Result-Codecov")
	.IsDependentOn("Create-NuGet-Package")
	.IsDependentOn("Upload-AppVeyor-Artifacts")
	.IsDependentOn("Publish-NuGet")
	.IsDependentOn("Publish-GitHub-Release")
    .Finally(() =>
{
    if (publishingError)
    {
         Warning("At least one exception occurred when executing non-essential tasks. These exceptions were ignored in order to allow the build script to complete.");
    }
});

Task("Default")
	.IsDependentOn("Run-Unit-Tests")
	.IsDependentOn("Create-NuGet-Package");


///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);



///////////////////////////////////////////////////////////////////////////////
// PRIVATE METHODS
///////////////////////////////////////////////////////////////////////////////
private static string TrimStart(this string source, string value, StringComparison comparisonType)
{
	if (source == null)
	{
		throw new ArgumentNullException(nameof(source));
	}

	int valueLength = value.Length;
	int startIndex = 0;
	while (source.IndexOf(value, startIndex, comparisonType) == startIndex)
	{
		startIndex += valueLength;
	}

	return source.Substring(startIndex);
}

private static List<string> ExecuteCommand(this ICakeContext context, FilePath exe, string args)
{
    context.StartProcess(exe, new ProcessSettings { Arguments = args, RedirectStandardOutput = true }, out var redirectedOutput);

    return redirectedOutput.ToList();
}

private static List<string> ExecGitCmd(this ICakeContext context, string cmd)
{
    var gitExe = context.Tools.Resolve(context.IsRunningOnWindows() ? "git.exe" : "git");
    return context.ExecuteCommand(gitExe, cmd);
}

private static string GetBuildBranch(this ICakeContext context)
{
    var buildSystem = context.BuildSystem();
    string repositoryBranch = null;

    if (buildSystem.IsRunningOnAppVeyor) repositoryBranch = buildSystem.AppVeyor.Environment.Repository.Branch;
    else if (buildSystem.IsRunningOnAzurePipelines) repositoryBranch = buildSystem.AzurePipelines.Environment.Repository.SourceBranchName;
    else if (buildSystem.IsRunningOnBamboo) repositoryBranch = buildSystem.Bamboo.Environment.Repository.Branch;
    else if (buildSystem.IsRunningOnBitbucketPipelines) repositoryBranch = buildSystem.BitbucketPipelines.Environment.Repository.Branch;
    else if (buildSystem.IsRunningOnBitrise) repositoryBranch = buildSystem.Bitrise.Environment.Repository.GitBranch;
    else if (buildSystem.IsRunningOnGitHubActions) repositoryBranch = buildSystem.GitHubActions.Environment.Workflow.Ref.Replace("refs/heads/", "");
    else if (buildSystem.IsRunningOnGitLabCI) repositoryBranch = buildSystem.GitLabCI.Environment.Build.RefName;
    else if (buildSystem.IsRunningOnTeamCity) repositoryBranch = buildSystem.TeamCity.Environment.Build.BranchName;
    else if (buildSystem.IsRunningOnTravisCI) repositoryBranch = buildSystem.TravisCI.Environment.Build.Branch;
	else repositoryBranch = ExecGitCmd(context, "rev-parse --abbrev-ref HEAD").Single();

    return repositoryBranch;
}

private static string GetRepoName(this ICakeContext context)
{
    var buildSystem = context.BuildSystem();

    if (buildSystem.IsRunningOnAppVeyor) return buildSystem.AppVeyor.Environment.Repository.Name;
    else if (buildSystem.IsRunningOnAzurePipelines) return buildSystem.AzurePipelines.Environment.Repository.RepoName;
    else if (buildSystem.IsRunningOnTravisCI) return buildSystem.TravisCI.Environment.Repository.Slug;
    else if (buildSystem.IsRunningOnGitHubActions) return buildSystem.GitHubActions.Environment.Workflow.Repository;

	var originUrl = ExecGitCmd(context, "config --get remote.origin.url").Single();
	var parts = originUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
	return $"{parts[parts.Length - 2]}/{parts[parts.Length - 1].Replace(".git", "")}";
}

private static void UpdateProjectTarget(this ICakeContext context, string path, string desiredTarget)
{
	var peekSettings = new XmlPeekSettings { SuppressWarning = true };
	foreach(var projectFile in context.GetFiles(path))
	{
		context.Information("Updating TFM in: {0}", projectFile.ToString());
		if (context.XmlPeek(projectFile, "/Project/PropertyGroup/TargetFramework", peekSettings) != null) context.XmlPoke(projectFile, "/Project/PropertyGroup/TargetFramework", desiredTarget);
		if (context.XmlPeek(projectFile, "/Project/PropertyGroup/TargetFrameworks", peekSettings) != null) context.XmlPoke(projectFile, "/Project/PropertyGroup/TargetFrameworks", desiredTarget);
	}
}
