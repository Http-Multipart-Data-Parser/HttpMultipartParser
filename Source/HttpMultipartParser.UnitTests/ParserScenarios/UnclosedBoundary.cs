using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class UnclosedBoundary
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"------51523
            Content-Disposition: form-data; name=""value""
              
            my value
            ------51523"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("value", "my value")
			},
			new List<FilePart>()
		);

		public UnclosedBoundary(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void DoesntInfiniteLoopOnUnclosedInput()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				// We expect this to throw!
				Assert.Throws<MultipartParseException>(() => MultipartFormDataParser.Parse(stream, options));
			}
		}

		[Fact]
		public async Task DoesntInfiniteLoopOnUnclosedInputAsync()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				// We expect this to throw!
				await Assert.ThrowsAsync<MultipartParseException>(() => MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken));
			}
		}
	}
}
