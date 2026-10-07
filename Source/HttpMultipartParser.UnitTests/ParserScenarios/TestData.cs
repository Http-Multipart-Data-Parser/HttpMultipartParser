using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	/// <summary>
	///     Represents a single parsing test case and the expected parameter/file outputs
	///     for a given request.
	/// </summary>
	internal class TestData
	{
		#region Constructors and Destructors

		public TestData(
			string request,
			List<ParameterPart> expectedParams,
			List<FilePart> expectedFileData)
		{
			Request = request;
			ExpectedParams = expectedParams;
			ExpectedFileData = expectedFileData;
		}

		#endregion

		#region Public Properties

		/// <summary>
		///     Gets or sets the expected file data.
		/// </summary>
		public List<FilePart> ExpectedFileData { get; set; }

		/// <summary>
		///     Gets or sets the expected parameters.
		/// </summary>
		public List<ParameterPart> ExpectedParams { get; set; }

		/// <summary>
		///     Gets or sets the request.
		/// </summary>
		public string Request { get; set; }

		#endregion

		#region Public Methods and Operators

		/// <summary>
		///     Validates the output of the parser against the expected outputs for
		///     this test
		/// </summary>
		/// <param name="parser">
		///     The parser to validate.
		/// </param>
		/// <returns>
		///     The <see cref="bool" /> representing if this test passed.
		/// </returns>
		public bool Validate(IMultipartFormDataParser parser, ITestOutputHelper outputHelper = null)
		{
			// Build a diagnostic message 
			outputHelper?.WriteLine("TestData.Validate details:");

			// Validate parameters
			var result = ValidateParameters(parser);
			outputHelper?.WriteLine("PARAMETERS");
			if (result)
			{
				outputHelper?.WriteLine("  Actual and expected match");
			}
			else
			{
				outputHelper?.WriteLine($"  Expected: {ExpectedParams.Count}");
				foreach (var p in ExpectedParams)
				{
					outputHelper?.WriteLine($"    Name='{p.Name}' Value='{p.Data}'");
				}
				outputHelper?.WriteLine($"  Actual: {parser.Parameters.Count}");
				foreach (var p in parser.Parameters)
				{
					outputHelper?.WriteLine($"    Name='{p.Name}' Value='{p.Data}'");
				}
			}

			// Validate files
			result &= ValidateFiles(parser);
			outputHelper?.WriteLine("FILES");
			if (result)
			{
				outputHelper?.WriteLine("  Actual and expected match");
			}
			else
			{
				outputHelper?.WriteLine($"  Expected: {ExpectedFileData.Count}");
				for (int i = 0; i < ExpectedFileData.Count; i++)
				{
					var f = ExpectedFileData[i];
					outputHelper?.WriteLine($"    [{i}] Name='{f.Name}' FileName='{f.FileName}' ContentType='{f.ContentType}' Size='{(f.Data.CanSeek ? f.Data.Length.ToString() : "unknown")}'");
				}
				outputHelper?.WriteLine($"  Actual: {parser.Files.Count}");
				for (int i = 0; i < parser.Files.Count; i++)
				{
					var f = parser.Files[i];
					outputHelper?.WriteLine($"    [{i}] Name='{f.Name}' FileName='{f.FileName}' ContentType='{f.ContentType}' Size='{(f.Data.CanSeek ? f.Data.Length.ToString() : "unknown")}'");
				}
			}

			if (!result)
			{
				// Build a diagnostic string to aid debugging and fail fast
				var sb = new System.Text.StringBuilder();
				sb.AppendLine("TestData.Validate failure:");
				if (ExpectedParams != null && ExpectedParams.Count > 0)
				{
					sb.AppendLine("Expected Parameters:");
					foreach (var p in ExpectedParams) sb.AppendLine($"  Name='{p.Name}' Value='{p.Data}'");
				}
				if (parser.Parameters != null && parser.Parameters.Count > 0)
				{
					sb.AppendLine("Actual Parameters:");
					foreach (var p in parser.Parameters) sb.AppendLine($"  Name='{p.Name}' Value='{p.Data}'");
				}
				sb.AppendLine("Expected Files:");
				for (int i = 0; i < ExpectedFileData.Count; i++)
				{
					var f = ExpectedFileData[i];
					sb.AppendLine($"  [{i}] Name='{f.Name}' FileName='{f.FileName}' ContentType='{f.ContentType}' Size='{(f.Data.CanSeek ? f.Data.Length.ToString() : "unknown")}'");
				}
				sb.AppendLine($"Actual Files: {parser.Files.Count}");
				for (int i = 0; i < parser.Files.Count; i++)
				{
					var f = parser.Files[i];
					sb.AppendLine($"  [{i}] Name='{f.Name}' FileName='{f.FileName}' ContentType='{f.ContentType}' Size='{(f.Data.CanSeek ? f.Data.Length.ToString() : "unknown")}'");
				}
				throw new InvalidOperationException(sb.ToString());
			}

			return true;
		}

		#endregion

		#region Private methods

		private bool ValidateParameters(IMultipartFormDataParser parser)
		{
			var actualParameters = parser.Parameters.GroupBy(p => p.Name);
			var expectedParameters = ExpectedParams.GroupBy(p => p.Name);

			// Make sure the number of actual parameters matches the number of expected parameters
			if (actualParameters.Count() != expectedParameters.Count()) return false;

			// Validate that each expected value has a corresponding actual value
			return actualParameters.Zip(expectedParameters, Tuple.Create).All(t =>
			{
				// Make sure the name of the actual parameter matches the name of the expected parameter
				if (t.Item1.Key != t.Item2.Key) return false;

				var actualValues = t.Item1.Select(i => i.Data);
				var expectedValues = t.Item2.Select(i => i.Data);

				// Make sure the number of actual values matches the number of expected values
				if (actualValues.Count() != expectedValues.Count()) return false;

				// Validate that each expected value has a corresponding actual value
				return actualValues.Zip(expectedValues, Tuple.Create).All(v => v.Item1 == v.Item2);
			});
		}

		private bool ValidateFiles(IMultipartFormDataParser parser)
		{
			// PLEASE NOTE: we can't rely on the name and/or the file name because they are not guaranteed to be unique.
			// Therefore we assume that the first expected file should match the first actual file,
			// the second expected file should match the second actual files, etc.

			if (ExpectedFileData.Count != parser.Files.Count) return false;

			for (int i = 0; i < ExpectedFileData.Count; i++)
			{
				var expectedFile = ExpectedFileData[i];
				var actualFile = parser.Files[i];

				if (expectedFile.Name != actualFile.Name) return false;
				if (expectedFile.FileName != actualFile.FileName) return false;
				if (expectedFile.ContentType != actualFile.ContentType) return false;
				if (expectedFile.ContentDisposition != actualFile.ContentDisposition) return false;

				if (expectedFile.AdditionalProperties.Count != actualFile.AdditionalProperties.Count) return false;
				if (expectedFile.AdditionalProperties.Any(pair => !actualFile.AdditionalProperties.Keys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))) return false;
				if (actualFile.AdditionalProperties.Any(pair => !expectedFile.AdditionalProperties.Keys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))) return false;
				if (expectedFile.AdditionalProperties.Any(pair => actualFile.AdditionalProperties[pair.Key] != pair.Value)) return false; // Case-sensitive

				// Read the data from the files and see if it's the same
				if (expectedFile.Data.CanSeek && expectedFile.Data.Position != 0) expectedFile.Data.Position = 0;
				if (actualFile.Data.CanSeek && actualFile.Data.Position != 0) actualFile.Data.Position = 0;

				string expectedFileData;
				// The last boolean parameter MUST be set to true: it ensures the stream is left open
				using (var reader = new StreamReader(expectedFile.Data, Encoding.UTF8, false, 1024, true))
				{
					expectedFileData = reader.ReadToEnd();
				}

				string actualFileData;
				// The last boolean parameter MUST be set to true: it ensures the stream is left open
				using (var reader = new StreamReader(actualFile.Data, Encoding.UTF8, false, 1024, true))
				{
					actualFileData = reader.ReadToEnd();
				}

				if (expectedFileData != actualFileData)
				{
					return false;
				}
			}

			return true;
		}

		#endregion
	}
}
