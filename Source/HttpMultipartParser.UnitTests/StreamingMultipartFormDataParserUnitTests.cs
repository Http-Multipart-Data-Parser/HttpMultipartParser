using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests
{
	/// <summary>
	///     Unit tests for StreamingMultipartFormDataParser.
	/// </summary>
	public class StreamingMultipartFormDataParserUnitTests
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
				var parser = new StreamingMultipartFormDataParser(stream, options);

				// Intentionally setting these handlers to null to verify that we can parse the stream despite missing handlers
				// See: https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/121
				parser.ParameterHandler = null;
				parser.FileHandler = null;

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
				var parser = new StreamingMultipartFormDataParser(stream, options);

				// Intentionally setting these handlers to null to verify that we can parse the stream despite missing handlers
				// See: https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/121
				parser.ParameterHandler = null;
				parser.FileHandler = null;

				await parser.RunAsync(TestContext.Current.CancellationToken);
			}
		}

		[Fact]
		public void CanHandleDefaultOptions()
		{
			using (Stream stream = TestUtil.StringToStream(_testData))
			{
				var parser = new StreamingMultipartFormDataParser(stream);

				// Intentionally setting these handlers to null to verify that we can parse the stream despite missing handlers
				// See: https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/121
				parser.ParameterHandler = null;
				parser.FileHandler = null;

				parser.Run();
			}
		}
	}
}
