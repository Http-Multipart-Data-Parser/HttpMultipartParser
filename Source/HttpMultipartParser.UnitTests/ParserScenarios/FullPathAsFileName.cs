using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class FullPathAsFileName
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"-----------------------------7de6cc440a46
            Content-Disposition: form-data; name=""file""; filename=""C:\test\test;abc.txt""
            Content-Type: text/plain

            test
            -----------------------------7de6cc440a46--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart>(),
			new List<FilePart> {
					new FilePart("file", "test;abc.txt", TestUtil.StringToStream("test"), "text/plain", "form-data")
			}
		);

		public FullPathAsFileName(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void HandlesFullPathAsFileNameWithSemicolonCorrectly()
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
		public async Task HandlesFullPathAsFileNameWithSemicolonCorrectlyAsync()
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
