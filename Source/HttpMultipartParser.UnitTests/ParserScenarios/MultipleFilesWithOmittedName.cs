using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	/// <summary>
	///     Test case for multiple files with omitted name.
	/// </summary>
	public class MultipleFilesWithOmittedName
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
            Content-Disposition: form-data; filename=""file1.txt"";
            Content-Type: text/plain

            THIS IS TEXT FILE 1
            --boundary
            Content-Disposition: form-data; filename=""file2.txt"";
            Content-Type: text/plain

            THIS IS TEXT FILE 2 !!!
            --boundary
            Content-Disposition: form-data; filename=""file3.txt"";
            Content-Type: text/plain

            This is text file 3 1234567890
            --boundary--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			Enumerable.Empty<ParameterPart>().ToList(),
			new List<FilePart> {
				new FilePart( null, "file1.txt", TestUtil.StringToStreamNoBom("THIS IS TEXT FILE 1")),
				new FilePart( null, "file2.txt", TestUtil.StringToStreamNoBom("THIS IS TEXT FILE 2 !!!")),
				new FilePart( null, "file3.txt", TestUtil.StringToStreamNoBom("This is text file 3 1234567890"))
			}
		);

		public MultipleFilesWithOmittedName(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		/// <summary>
		///     Checks that multiple files don't get in the way of parsing each other
		///     and that everything parses correctly.
		/// </summary>
		[Fact]
		public void MultipleFilesWithOmittedNameTest()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = 16,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		[Fact]
		public async Task MultipleFilesWithOmittedNameAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = 16,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}
	}
}
