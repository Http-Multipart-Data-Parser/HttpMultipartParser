#if NET10_0_OR_GREATER
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests
{
	/// <summary>
	/// Unit tests for StreamingBinaryMultipartFormDataParserPipelines (pipeline prototype).
	/// These tests are intentionally lightweight and mirror the StreamingMultipartFormDataParserUnitTests behavior.
	/// </summary>
	public class StreamingBinaryMultipartFormDataParserPipelinesUnitTests
	{
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
			Content-Disposition: form-data; name=""parameter1""

			This is a sample parameter
			--boundary
			Content-Disposition: form-data; name=""file1""; filename=""file1.txt""
			Content-Type: text/plain

			This is the content of a sample file
			--boundary--"
		);

		[Fact]
		public void CanHandleNullDelegates()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testData, options.Encoding))
			{
				var parser = new StreamingBinaryMultipartFormDataParserPipelines(stream, options);

				// Intentionally set handlers to null to ensure parser tolerates missing delegates
				parser.ParameterHandler = null;
				parser.FileHandler = null;
				parser.StreamClosedHandler = null;

				parser.Run();
			}
		}

		[Fact]
		public async Task CanHandleNullDelegatesAsync()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testData, options.Encoding))
			{
				var parser = new StreamingBinaryMultipartFormDataParserPipelines(stream, options);

				// Intentionally setting these handlers to null to verify that we can parse the stream despite missing handlers
				parser.ParameterHandler = null;
				parser.FileHandler = null;
				parser.StreamClosedHandler = null;

				await parser.RunAsync(TestContext.Current.CancellationToken);
			}
		}

		[Fact]
		public void StreamClosedHandler_IsInvoked_ForRun()
		{
			using (Stream stream = TestUtil.StringToStream(_testData))
			{
				var parser = new StreamingBinaryMultipartFormDataParserPipelines(stream);

				bool closed = false;
				parser.StreamClosedHandler += () => closed = true;

				parser.Run();

				Assert.True(closed);
			}
		}

		[Fact]
		public async Task StreamClosedHandler_IsInvoked_ForRunAsync()
		{
			using (Stream stream = TestUtil.StringToStream(_testData))
			{
				var parser = new StreamingBinaryMultipartFormDataParserPipelines(stream);

				bool closed = false;
				parser.StreamClosedHandler += () => closed = true;

				await parser.RunAsync(TestContext.Current.CancellationToken);

				Assert.True(closed);
			}
		}
	}
}
#endif
