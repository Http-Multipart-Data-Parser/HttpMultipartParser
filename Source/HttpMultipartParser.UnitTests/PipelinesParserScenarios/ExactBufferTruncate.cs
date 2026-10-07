using HttpMultipartParser.UnitTests.ParserScenarios;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.PipelinesParserScenarios
{
	/// <summary>
	///     Tests that the final '--' ending up in a seperate chunk doesn't break everything.
	/// </summary>
	public class ExactBufferTruncate
	{
		private readonly ITestOutputHelper _outputHelper;

		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
            Content-Disposition: form-data; name=""text""

            textdata
            --boundary
            Content-Disposition: form-data; name=""file""; filename=""data.txt""
            Content-Type: text/plain

            1234567890123456789012
            --boundary--"
		);

		/// <summary>
		///     This test has the buffer split such that the final '--' of the end boundary
		///     falls into the next buffer.
		/// </summary>
		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("text", "textdata")
			},
			new List<FilePart> {
				new FilePart( "file", "data.txt", TestUtil.StringToStreamNoBom("1234567890123456789012"))
			}
		);

		public ExactBufferTruncate(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		/// <summary>
		///     Tests that the final '--' ending up in a seperate chunk doesn't break everything.
		/// </summary>
		[Fact]
		public void CanHandleFinalDashesInSeperateBufferFromEndBinary()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = 16,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParserPipelines.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Tests that the final '--' ending up in a seperate chunk doesn't break everything.
		/// </summary>
		[Fact]
		public async Task CanHandleFinalDashesInSeperateBufferFromEndBinaryAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = 16,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParserPipelines.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}
	}
}
