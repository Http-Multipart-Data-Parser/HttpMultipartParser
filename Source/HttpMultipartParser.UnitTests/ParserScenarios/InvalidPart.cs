using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class InvalidPart
	{
		private readonly ITestOutputHelper _outputHelper;
		// For details see: https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/110
		// This data is considered invalid because it contains nothing but empty lines
		private static readonly string _testData = @"--KoZIhvcNAQcB

--KoZIhvcNAQcB--";

		private static readonly TestData _testCase = new TestData(
			_testData,
			Enumerable.Empty<ParameterPart>().ToList(),
			Enumerable.Empty<FilePart>().ToList()
		);

		public InvalidPart(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void Exception_is_thrown_when_attempting_to_parse()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8,
			};

			// The default behavior is to throw an exception when the form contains an invalid section.
			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				Assert.Throws<MultipartParseException>(() => MultipartFormDataParser.Parse(stream, options));
			}
		}

		[Fact]
		public async Task Exception_is_thrown_when_attempting_to_parse_async()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8,
			};

			// The default behavior is to throw an exception when the form contains an invalid section.
			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				await Assert.ThrowsAsync<MultipartParseException>(() => MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken));
			}
		}

		[Fact]
		public void Invalid_part_is_ignored()
		{
			var options = new ParserOptions
			{
				IgnoreInvalidParts = true
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.Empty(parser.Files);
				Assert.Empty(parser.Parameters);
			}
		}

		[Fact]
		public async Task Invalid_part_is_ignored_async()
		{
			var options = new ParserOptions
			{
				IgnoreInvalidParts = true
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.Empty(parser.Files);
				Assert.Empty(parser.Parameters);
			}
		}
	}
}
