using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	/// <summary>
	///     Test for cases where the file is last with expected outcomes.
	/// </summary>
	public class FileIsLast
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"-----------------------------41952539122868
            Content-Disposition: form-data; name=""adID""

            1425
            -----------------------------41952539122868
            Content-Disposition: form-data; name=""files[]""; filename=""Capture.JPG""
            Content-Type: image/jpeg

            BinaryData
            -----------------------------41952539122868--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("adID", "1425")
			},
			new List<FilePart> {
				new FilePart("files[]", "Capture.JPG", TestUtil.StringToStreamNoBom("BinaryData"), "image/jpeg", "form-data")
			}
		);

		public FileIsLast(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void CanHandleFileAsLastSection()
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
		public async Task CanHandleFileAsLastSectionAsync()
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
