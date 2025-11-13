using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests
{
	/// <summary>
	///     Summary description for RebufferableBinaryReaderUnitTest
	/// </summary>
	public class RebufferableBinaryReaderUnitTests
	{
		#region Read() Tests

		[Fact]
		public void CanReadSingleCharacterBuffer()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("abc"), Encoding.UTF8);

			Assert.Equal('a', reader.Read());
			Assert.Equal('b', reader.Read());
			Assert.Equal('c', reader.Read());
		}

		[Fact]
		public void CanReadSingleCharacterOverBuffers()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("def"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom("abc"));

			Assert.Equal('a', reader.Read());
			Assert.Equal('b', reader.Read());
			Assert.Equal('c', reader.Read());
			Assert.Equal('d', reader.Read());
			Assert.Equal('e', reader.Read());
			Assert.Equal('f', reader.Read());
		}

		[Fact]
		public void CanReadMixedAsciiAndUTFCharacters()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("abcdèfg"), Encoding.UTF8);

			Assert.Equal('a', reader.Read());
			Assert.Equal('b', reader.Read());
			Assert.Equal('c', reader.Read());
			Assert.Equal('d', reader.Read());
			Assert.Equal('è', reader.Read());
			Assert.Equal('f', reader.Read());
			Assert.Equal('g', reader.Read());
		}

		[Fact]
		public void CanReadMixedAsciiAndUTFCharactersOverBuffers()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("dèfg"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom("abc"));

			Assert.Equal('a', reader.Read());
			Assert.Equal('b', reader.Read());
			Assert.Equal('c', reader.Read());
			Assert.Equal('d', reader.Read());
			Assert.Equal('è', reader.Read());
			Assert.Equal('f', reader.Read());
			Assert.Equal('g', reader.Read());
		}

		[Theory]
		[InlineData(new byte[] { 0xef, 0xbb, 0xbf }, "utf-8")] // UTF-8
		[InlineData(new byte[] { 0xff, 0xfe }, "utf-16")] // UTF-16
		[InlineData(new byte[] { 0xfe, 0xff }, "utf-16BE")] // UTF-16 Big Endian
		[InlineData(new byte[] { 0xfe, 0xff }, "utf-32")] // UTF-32
		[InlineData(new byte[] { 0x00, 0x00, 0xfe, 0xff }, "utf-32BE")] // UTF-32 Big Endian
		public void CanReadBOM(byte[] bom, string encodingName)
		{
			var encoding = Encoding.GetEncoding(encodingName);

			var prefixString = "Foo Bar";
			var prefixBinary = encoding.GetBytes(prefixString);

			var sufixString = "Hello world";
			var sufixBinary = encoding.GetBytes(sufixString);

			var stream = new MemoryStream();
			var binaryWriter = new BinaryWriter(stream);
			binaryWriter.Write(prefixBinary);
			binaryWriter.Write(bom);
			binaryWriter.Write(sufixBinary);
			binaryWriter.Flush();
			stream.Position = 0;

			var reader = new RebufferableBinaryReader(stream, encoding);

			var buffer = new byte[prefixBinary.Length];
			reader.Read(buffer, 0, buffer.Length);
			Assert.Equal(prefixString, encoding.GetString(buffer));

			buffer = new byte[bom.Length];
			reader.Read(buffer, 0, buffer.Length);
			for (int i = 0; i < buffer.Length; i++)
			{
				Assert.Equal(bom[i], buffer[i]);
			}

			buffer = new byte[sufixBinary.Length];
			reader.Read(buffer, 0, buffer.Length);
			Assert.Equal(sufixString, encoding.GetString(buffer));
		}

		#endregion

		#region ReadAsync() Tests

		[Fact]
		public async Task CanReadSingleCharacterBufferAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("abc"), Encoding.UTF8);

			Assert.Equal('a', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('b', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('c', await reader.ReadAsync(TestContext.Current.CancellationToken));
		}

		[Fact]
		public async Task CanReadSingleCharacterOverBuffersAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("def"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom("abc"));

			Assert.Equal('a', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('b', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('c', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('d', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('e', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('f', await reader.ReadAsync(TestContext.Current.CancellationToken));
		}

		[Fact]
		public async Task CanReadMixedAsciiAndUTFCharactersAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("abcdèfg"), Encoding.UTF8);

			Assert.Equal('a', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('b', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('c', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('d', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('è', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('f', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('g', await reader.ReadAsync(TestContext.Current.CancellationToken));
		}

		[Fact]
		public async Task CanReadMixedAsciiAndUTFCharactersOverBuffersAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("dèfg"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom("abc"));

			Assert.Equal('a', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('b', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('c', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('d', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('è', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('f', await reader.ReadAsync(TestContext.Current.CancellationToken));
			Assert.Equal('g', await reader.ReadAsync(TestContext.Current.CancellationToken));
		}

		#endregion

		#region Read(buffer, index, count) Tests

		[Fact]
		public void CanReadSingleBuffer()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[Encoding.UTF8.GetByteCount("6chars")];
			reader.Read(buffer, 0, buffer.Length);
			string result = Encoding.UTF8.GetString(buffer);
			Assert.Equal("6chars", result);
		}

		[Fact]
		public void CanReadAcrossMultipleBuffers()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("ars"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom("6ch"));

			var buffer = new byte[6];
			reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("6chars", Encoding.UTF8.GetString(buffer));
		}

		[Fact]
		public void CanReadMixedAsciiAndUTF8()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("5èats"), Encoding.UTF8);

			var buffer = new byte[Encoding.UTF8.GetByteCount("5èats")];
			reader.Read(buffer, 0, buffer.Length);
			string result = Encoding.UTF8.GetString(buffer);
			Assert.Equal("5èats", result);
		}

		[Fact]
		public void CanReadMixedAsciiAndUTF8AcrossMultipleBuffers()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("ts"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom(("5èa")));

			var buffer = new byte[Encoding.UTF8.GetByteCount("5èats")];
			reader.Read(buffer, 0, buffer.Length);
			string result = Encoding.UTF8.GetString(buffer);
			Assert.Equal("5èats", result);
		}

		[Fact]
		public void ReadCorrectlyHandlesSmallerBufferThenStream()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[4];
			reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("6cha", Encoding.UTF8.GetString(buffer));

			buffer = new byte[2];
			reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("rs", Encoding.UTF8.GetString(buffer));
		}

		[Fact]
		public void ReadCorrectlyHandlesLargerBufferThenStream()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[10];
			int amountRead = reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("6chars\0\0\0\0", Encoding.UTF8.GetString(buffer));
			Assert.Equal(6, amountRead);
		}

		[Fact]
		public void ReadReturnsZeroOnNoData()
		{
			var reader = new RebufferableBinaryReader(new MemoryStream(), Encoding.UTF8);

			var buffer = new byte[6];
			int amountRead = reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("\0\0\0\0\0\0", Encoding.UTF8.GetString(buffer));
			Assert.Equal(0, amountRead);
		}

		[Fact]
		public void ReadLineReturnsNullOnNoData()
		{
			var reader = new RebufferableBinaryReader(new MemoryStream(new byte[6]), Encoding.UTF8);

			var s = reader.ReadLine();
			Assert.Equal("\0\0\0\0\0\0", s);
			Assert.Null(reader.ReadLine());
		}

		[Fact]
		public void ReadCanResumeInterruptedStream()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[4];
			int amountRead = reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("6cha", Encoding.UTF8.GetString(buffer));
			Assert.Equal(4, amountRead);

			reader.Buffer(TestUtil.StringToByteNoBom("14intermission"));
			buffer = new byte[14];
			amountRead = reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("14intermission", Encoding.UTF8.GetString(buffer));
			Assert.Equal(14, amountRead);

			buffer = new byte[2];
			amountRead = reader.Read(buffer, 0, buffer.Length);
			Assert.Equal("rs", Encoding.UTF8.GetString(buffer));
			Assert.Equal(2, amountRead);
		}

		#endregion

		#region ReadAsync(buffer, index, count) Tests

		[Fact]
		public async Task CanReadSingleBufferAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[Encoding.UTF8.GetByteCount("6chars")];
			await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			string result = Encoding.UTF8.GetString(buffer);
			Assert.Equal("6chars", result);
		}

		[Fact]
		public async Task CanReadAcrossMultipleBuffersAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("ars"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom("6ch"));

			var buffer = new byte[6];
			await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("6chars", Encoding.UTF8.GetString(buffer));
		}

		[Fact]
		public async Task CanReadMixedAsciiAndUTF8Async()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("5èats"), Encoding.UTF8);

			var buffer = new byte[Encoding.UTF8.GetByteCount("5èats")];
			await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			string result = Encoding.UTF8.GetString(buffer);
			Assert.Equal("5èats", result);
		}

		[Fact]
		public async Task CanReadMixedAsciiAndUTF8AcrossMultipleBuffersAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("ts"), Encoding.UTF8);
			reader.Buffer(TestUtil.StringToByteNoBom(("5èa")));

			var buffer = new byte[Encoding.UTF8.GetByteCount("5èats")];
			await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			string result = Encoding.UTF8.GetString(buffer);
			Assert.Equal("5èats", result);
		}

		[Fact]
		public async Task ReadCorrectlyHandlesSmallerBufferThenStreamAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[4];
			await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("6cha", Encoding.UTF8.GetString(buffer));

			buffer = new byte[2];
			await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("rs", Encoding.UTF8.GetString(buffer));
		}

		[Fact]
		public async Task ReadCorrectlyHandlesLargerBufferThenStreamAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[10];
			int amountRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("6chars\0\0\0\0", Encoding.UTF8.GetString(buffer));
			Assert.Equal(6, amountRead);
		}

		[Fact]
		public async Task ReadReturnsZeroOnNoDataAsync()
		{
			var reader = new RebufferableBinaryReader(new MemoryStream(), Encoding.UTF8);

			var buffer = new byte[6];
			int amountRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("\0\0\0\0\0\0", Encoding.UTF8.GetString(buffer));
			Assert.Equal(0, amountRead);
		}

		[Fact]
		public async Task ReadLineReturnsNullOnNoDataAsync()
		{
			var reader = new RebufferableBinaryReader(new MemoryStream(new byte[6]), Encoding.UTF8);

			var s = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
			Assert.Equal("\0\0\0\0\0\0", s);
			Assert.Null(await reader.ReadLineAsync(TestContext.Current.CancellationToken));
		}

		[Fact]
		public async Task ReadCanResumeInterruptedStreamAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("6chars"), Encoding.UTF8);

			var buffer = new byte[4];
			int amountRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("6cha", Encoding.UTF8.GetString(buffer));
			Assert.Equal(4, amountRead);

			reader.Buffer(TestUtil.StringToByteNoBom("14intermission"));
			buffer = new byte[14];
			amountRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("14intermission", Encoding.UTF8.GetString(buffer));
			Assert.Equal(14, amountRead);

			buffer = new byte[2];
			amountRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal("rs", Encoding.UTF8.GetString(buffer));
			Assert.Equal(2, amountRead);
		}

		#endregion

		#region ReadByteLine() Tests

		[Fact]
		// This unit test verifies that ReadByteLine can read a line which spans multiple chunks.
		// https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/40
		public void CanReadByteLineAccrossChunks()
		{
			// For this test to truly demonstrate that we can read a line which spans multiple chunks,
			// the newline needs to be at a position greater than the buffer size.
			var bufferSize = 5;
			var inputString = "0123456789ab\r\ncde";

			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom(inputString), Encoding.UTF8, bufferSize);
			var bytes = reader.ReadByteLine();
			var expected = Encoding.UTF8.GetBytes("0123456789ab");

			foreach (var pair in expected.Zip(bytes, Tuple.Create))
			{
				Assert.Equal(pair.Item1, pair.Item2);
			}
		}

		[Fact]
		public void CanReadByteLineOnMixedAsciiAndUTF8Text()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("Bonjour poignée"), Encoding.UTF8);
			byte[] bytes = reader.ReadByteLine();
			var expected = new byte[] { 66, 111, 110, 106, 111, 117, 114, 32, 112, 111, 105, 103, 110, 195, 169, 101 };

			foreach (var pair in expected.Zip(bytes, Tuple.Create))
			{
				Assert.Equal(pair.Item1, pair.Item2);
			}
		}

		#endregion

		#region ReadByteLineAsync() Tests

		[Fact]
		// This unit test verifies that ReadByteLine can read a line which spans multiple chunks.
		// https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/40
		public async Task CanReadByteLineAsyncAccrossChunks()
		{
			// For this test to truly demonstrate that we can read a line which spans multiple chunks,
			// the newline needs to be at a position greater than the buffer size.
			var bufferSize = 5;
			var inputString = "0123456789ab\r\ncde";

			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom(inputString), Encoding.UTF8, bufferSize);
			var bytes = await reader.ReadByteLineAsync(TestContext.Current.CancellationToken);
			var expected = Encoding.UTF8.GetBytes("0123456789ab");

			foreach (var pair in expected.Zip(bytes, Tuple.Create))
			{
				Assert.Equal(pair.Item1, pair.Item2);
			}
		}

		[Fact]
		public async Task CanReadByteLineOnMixedAsciiAndUTF8TextAsync()
		{
			var reader = new RebufferableBinaryReader(TestUtil.StringToStreamNoBom("Bonjour poignée"), Encoding.UTF8);
			byte[] bytes = await reader.ReadByteLineAsync(TestContext.Current.CancellationToken);
			var expected = new byte[] { 66, 111, 110, 106, 111, 117, 114, 32, 112, 111, 105, 103, 110, 195, 169, 101 };

			foreach (var pair in expected.Zip(bytes, Tuple.Create))
			{
				Assert.Equal(pair.Item1, pair.Item2);
			}
		}

		#endregion
	}
}
