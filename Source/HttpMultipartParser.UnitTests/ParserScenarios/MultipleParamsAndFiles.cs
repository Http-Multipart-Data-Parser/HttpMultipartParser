using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	/// <summary>
	///     Test case for multiple parameters and files.
	/// </summary>
	public class MultipleParamsAndFiles
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
            Content-Disposition: form-data; name=""text""
              
            textdata
            --boundary
            Content-Disposition: form-data; name=""after"";TestForTextWithoutSplit
              
            afterdata
            --boundary
            Content-Disposition: form-data; name=""file""; filename=""data.txt""
            Content-Type: text/plain

            I am the first data 
            --boundary
            Content-Disposition: form-data;TestForTextWithoutSplit; name=""newfile""; filename=""superdata.txt""
            Content-Type: text/plain

            I am the second data
            --boundary
            Content-Disposition: form-data; name=""never""

            neverdata 
            --boundary
            Content-Disposition: form-data; name=""waylater""

            waylaterdata 
            --boundary--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("text", "textdata"),
				new ParameterPart("after", "afterdata"),
				new ParameterPart("never", "neverdata"),
				new ParameterPart("waylater", "waylaterdata")
			},
			new List<FilePart> {
				new FilePart( "file", "data.txt", TestUtil.StringToStreamNoBom("I am the first data")),
				new FilePart( "newfile", "superdata.txt", TestUtil.StringToStreamNoBom("I am the second data"))
			}
		);

		public MultipleParamsAndFiles(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		/// <summary>
		///     Checks that multiple files don't get in the way of parsing each other
		///     and that everything parses correctly.
		/// </summary>
		[Fact]
		public void MultipleFilesAndParamsTest()
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
		public async Task MultipleFilesAndParamsTestAsync()
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
