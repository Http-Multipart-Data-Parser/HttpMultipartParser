using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	/// <summary>
	///     Test case for multiple files with same name.
	/// </summary>
	public class MultipleFilesWithSameName
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
            Content-Disposition: form-data; name=""file1.txt"";filename=""file1.txt"";
            Content-Type: text/plain

            THIS IS TEXT FILE 1
            --boundary
            Content-Disposition: form-data; name=""file2.txt"";filename=""file2.txt"";
            Content-Type: text/plain

            THIS IS TEXT FILE 2 !!!
            --boundary
            Content-Disposition: form-data; name=""file2.txt"";filename=""file2.txt"";
            Content-Type: text/plain

            This is text file 3 1234567890
            --boundary--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			Enumerable.Empty<ParameterPart>().ToList(),
			new List<FilePart> {
				new FilePart( "file1.txt", "file1.txt", TestUtil.StringToStreamNoBom("THIS IS TEXT FILE 1")),
				new FilePart( "file2.txt", "file2.txt", TestUtil.StringToStreamNoBom("THIS IS TEXT FILE 2 !!!")),
				new FilePart( "file2.txt", "file2.txt", TestUtil.StringToStreamNoBom("This is text file 3 1234567890"))
			}
		);

		public MultipleFilesWithSameName(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		/// <summary>
		///     Checks that multiple files don't get in the way of parsing each other
		///     and that everything parses correctly.
		/// </summary>
		[Fact]
		public void MultipleFilesWithSameNameTest()
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
		public async Task MultipleFilesWithSameNameTestAsync()
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
