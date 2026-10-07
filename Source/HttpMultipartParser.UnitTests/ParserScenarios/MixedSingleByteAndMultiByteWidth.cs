using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class MixedSingleByteAndMultiByteWidth
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"-----------------------------41952539122868
            Content-Disposition: form-data; name=""تت""

            1425
            -----------------------------41952539122868
            Content-Disposition: form-data; name=""files[]""; filename=""تست.jpg""
            Content-Type: image/jpeg

            BinaryData
            -----------------------------41952539122868--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("تت", "1425")
			},
			new List<FilePart> {
				new FilePart("files[]", "تست.jpg", TestUtil.StringToStreamNoBom("BinaryData"), "image/jpeg", "form-data")
			}
		);

		public MixedSingleByteAndMultiByteWidth(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void CanHandleMixedSingleByteAndMultiByteWidthCharacters()
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
		public async Task CanHandleMixedSingleByteAndMultiByteWidthCharactersAsync()
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
