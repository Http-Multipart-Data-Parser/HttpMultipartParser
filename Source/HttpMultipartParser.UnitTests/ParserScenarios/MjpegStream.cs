using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class MjpegStream
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--MOBOTIX_Fast_Serverpush
            Content-Type: image/jpeg

            <jpeg bytes>
            --MOBOTIX_Fast_Serverpush
            Content-Type: image/jpeg

            <jpeg bytes>
            --MOBOTIX_Fast_Serverpush
            Content-Type: image/jpeg

            ... and so on ...
            --MOBOTIX_Fast_Serverpush--"
		);

		/// <summary>
		///     Test case for mjpeg stream.
		/// </summary>
		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> { },
			new List<FilePart> {
				new FilePart(null, null, TestUtil.StringToStreamNoBom("<jpeg bytes>"), "image/jpeg", "form-data"),
				new FilePart(null, null, TestUtil.StringToStreamNoBom("<jpeg bytes>"), "image/jpeg", "form-data"),
				new FilePart(null, null, TestUtil.StringToStreamNoBom("... and so on ..."), "image/jpeg", "form-data")
			}
	   );

		/// <summary>
		///     Initializes the test data before each run, this primarily
		///     consists of resetting data stream positions.
		/// </summary>
		public MjpegStream(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void MjpegStreamTest()
		{
			var options = new ParserOptions
			{
				Boundary = "MOBOTIX_Fast_Serverpush",
				BinaryBufferSize = 32,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		[Fact]
		public async Task MjpegStreamTest_Async()
		{
			var options = new ParserOptions
			{
				Boundary = "MOBOTIX_Fast_Serverpush",
				BinaryBufferSize = 32,
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
