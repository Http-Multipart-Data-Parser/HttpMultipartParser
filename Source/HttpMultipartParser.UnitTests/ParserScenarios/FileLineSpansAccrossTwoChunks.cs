using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	// This test attempts to reproduce the bug discussed here:
	// https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/40
	public class FileLineSpansAccrossTwoChunks
	{
		private static readonly string part1 = "--boundary\r\nContent-Disposition: form-data; name=\"param1\"\r\n\r\nFirst value\r\n";
		private static readonly string part2 = "--boundary\r\nContent-Disposition: form-data; name=\"param2\"\r\n\r\nSecond value\r\n--boundary--";

		// Buffer size is calculated to split the '\r' and '\n' after "First value"
		private static readonly int _binaryBufferSize = part1.Length - 1;
		private static readonly string _testData = TestUtil.TrimAllLines($"{part1}{part2}");

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart>
			{
				new ParameterPart("param1", "First value"),
				new ParameterPart("param2", "Second value"),
			},
			Enumerable.Empty<FilePart>().ToList()
		);

		/// <summary>
		///     Initializes the test data before each run, this primarily
		///     consists of resetting data stream positions.
		/// </summary>
		public FileLineSpansAccrossTwoChunks()
		{
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		[Fact]
		public void CanHandleNewLineAccrossTwoChunks()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = _binaryBufferSize,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser));
			}
		}
	}
}
