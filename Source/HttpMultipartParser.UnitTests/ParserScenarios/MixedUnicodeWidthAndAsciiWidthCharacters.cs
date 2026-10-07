using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class MixedUnicodeWidthAndAsciiWidthCharacters
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary_.oOo._MjQ1NTU=OTk3Ng==MjcxODE=
            Content-Disposition: form-data; name=""psAdTitle""

            Bonjour poignée 
            --boundary_.oOo._MjQ1NTU=OTk3Ng==MjcxODE=--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("psAdTitle", "Bonjour poignée")
			},
			new List<FilePart>()
		);

		public MixedUnicodeWidthAndAsciiWidthCharacters(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void CanHandleUnicodeWidthAndAsciiWidthCharacters()
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
		public async Task CanHandleUnicodeWidthAndAsciiWidthCharactersAsync()
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
