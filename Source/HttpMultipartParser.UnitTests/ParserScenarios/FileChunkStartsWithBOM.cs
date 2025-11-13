using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	// This test attempts to reproduce the bug discussed here:
	// https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/64
	public class FileChunkStartsWithBOM
	{
		private static readonly int _binaryBufferSize = 100;
		private static readonly byte[] _utf8BOMBinary = new byte[] { 0xef, 0xbb, 0xbf };
		private static readonly string _utf8BOMString = Encoding.UTF8.GetString(_utf8BOMBinary);

		private static readonly string _prefix =
	@"--boundary
Content-Type: application/octet-stream

";
		// This padding ensures that the BOM is positioned at the begining of the second chunk
		private static readonly string _padding = new string('+', _binaryBufferSize - _prefix.Length);
		private static readonly string _fileContent = $"{_padding}{_utf8BOMString}Hello world";

		private static readonly string _testDataFormat =
	@"{0}{1}
--boundary--";
		private static readonly string _testData = TestUtil.TrimAllLines(string.Format(_testDataFormat, _prefix, _fileContent));

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> { },
			new List<FilePart> {
				new FilePart(null, null, TestUtil.StringToStreamNoBom(_fileContent), null, "application/octet-stream", "form-data")
			}
		);

		/// <summary>
		///     Initializes the test data before each run, this primarily
		///     consists of resetting data stream positions.
		/// </summary>
		public FileChunkStartsWithBOM()
		{
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void CanHandleBOM()
		{
			var encoding = Encoding.UTF8;

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, encoding, _binaryBufferSize);

				using (var file = parser.Files[0].Data)
				{
					file.Position = 0;

					// Read the padding and assert we get the expected value
					var paddingBuffer = new byte[_padding.Length];
					var readCount = file.Read(paddingBuffer, 0, paddingBuffer.Length);

					Assert.Equal(_padding, encoding.GetString(paddingBuffer));

					// Read the BOM and assert we get the expected value
					var bomBuffer = new byte[_utf8BOMBinary.Length];
					readCount = file.Read(bomBuffer, 0, bomBuffer.Length);

					// If this assertion fails, it means that we have reproduced the problem described in GH-64
					// If it succeeds, it means that the bug has been fixed.
					Assert.Equal(_utf8BOMString, encoding.GetString(bomBuffer));

					// Read the rest of the content and assert we get the expected value
					var restOfContentBuffer = new byte[_fileContent.Length - _padding.Length - _utf8BOMString.Length];
					readCount = file.Read(restOfContentBuffer, 0, restOfContentBuffer.Length);

					Assert.Equal("Hello world", encoding.GetString(restOfContentBuffer));
				}
			}
		}
	}
}
