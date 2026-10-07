using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class SeveralValuesWithSameProperty
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"------B6u9lJxB4ByPiGPZ
            Content-Disposition: form-data; name=""options""

            value0
            ------B6u9lJxB4ByPiGPZ
            Content-Disposition: form-data; name=""options""

            value1
            ------B6u9lJxB4ByPiGPZ
            Content-Disposition: form-data; name=""options""

            value2
            ------B6u9lJxB4ByPiGPZ--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("options", "value0"),
				new ParameterPart("options", "value1"),
				new ParameterPart("options", "value2")
			},
			new List<FilePart>()
		);

		public SeveralValuesWithSameProperty(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void AcceptSeveralValuesWithSameProperty()
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
		public async Task AcceptSeveralValuesWithSamePropertyAsync()
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
