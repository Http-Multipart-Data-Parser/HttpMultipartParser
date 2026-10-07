using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class FilenameStar
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
            Content-Disposition: form-data; name=""file1""; filename=""data.txt""; filename*=""iso-8859-1'en'%A3%20rates.txt"";
            Content-Type: text/plain

            In this scenario, the filename* is preferred and filename is ignored.
			--boundary
            Content-Disposition: form-data; name=""file2""; filename*=""UTF-8''%c2%a3%20and%20%e2%82%ac%20rates.txt"";
            Content-Type: text/plain

            In this scenario, only the filename* is provided.
			--boundary
            Content-Disposition: form-data; name=""file3""; filename=""Pr�sentation_Export.zip""; filename*=""utf-8''Pr%C3%A4sentation_Export.zip"";
            Content-Type: text/plain

            This is the sample provided by @alexeyzimarev.
            --boundary--"
		);

		/// <summary>
		///     Test case for files with filename*.
		/// </summary>
		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> { },
			new List<FilePart> {
				new FilePart("file1", "£ rates.txt", TestUtil.StringToStreamNoBom("In this scenario, the filename* is preferred and filename is ignored."), "text/plain", "form-data"),
				new FilePart("file2", "£ and € rates.txt", TestUtil.StringToStreamNoBom("In this scenario, only the filename* is provided."), "text/plain", "form-data"),
				new FilePart("file3", "Präsentation_Export.zip", TestUtil.StringToStreamNoBom("This is the sample provided by @alexeyzimarev."), "text/plain","form-data")
			}
	   );

		/// <summary>
		///     Initializes the test data before each run, this primarily
		///     consists of resetting data stream positions.
		/// </summary>
		public FilenameStar(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void FilenameStarTest()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		[Fact]
		public async Task FilenameStarTest_Async()
		{
			var options = new ParserOptions
			{
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
