using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpMultipartParser
{
	/// <summary>
	///     Provides methods to parse a
	///     <see href="http://www.ietf.org/rfc/rfc2388.txt">
	///         <c>multipart/form-data</c>
	///     </see>
	///     stream into it's parameters and file data.
	/// </summary>
	/// <remarks>
	///     <para>
	///         A parameter is defined as any non-file data passed in the multipart stream. For example
	///         any form fields would be considered a parameter.
	///     </para>
	///     <para>
	///         The parser determines if a section is a file or not based on the presence or absence
	///         of the filename argument for the Content-Type header. If filename is set then the section
	///         is assumed to be a file, otherwise it is assumed to be parameter data.
	///     </para>
	///     <para>
	///          Please note that this parser is very similar to <seealso cref="StreamingMultipartFormDataParser"/>.
	///          The main difference being that this parser will read the content of parameters as binary rather than text.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code lang="C#">
	///       Stream multipartStream = GetTheMultipartStream();
	///       string boundary = GetTheBoundary();
	///       var parser = new StreamingMultipartFormDataParser(multipartStream, boundary, Encoding.UTF8);
	///
	///       // Set up our delegates for how we want to handle recieved data.
	///       // In our case parameters will be written to a dictionary and files
	///       // will be written to a filestream
	///       parser.ParameterHandler += parameter => AddToDictionary(parameter);
	///       parser.FileHandler += (name, fileName, type, disposition, buffer, bytes) => WriteDataToFile(fileName, buffer, bytes);
	///       parser.Run();
	///   </code>
	/// </example>
	public class StreamingBinaryMultipartFormDataParser : IStreamingBinaryMultipartFormDataParser
	{
		/// <summary>
		/// The stream we are parsing.
		/// </summary>
		private readonly Stream _stream;

		/// <summary>
		/// The options for the parser.
		/// </summary>
		private readonly ParserOptions _options;

		/// <summary>
		///     The boundary of the multipart message as a string.
		/// </summary>
		private string boundary;

		/// <summary>
		///     The boundary of the multipart message as a byte string
		///     encoded with CurrentEncoding.
		/// </summary>
		private byte[] boundaryBinary;

		/// <summary>
		///     The end boundary of the multipart message as a string.
		/// </summary>
		private string endBoundary;

		/// <summary>
		///     The end boundary of the multipart message as a byte string
		///     encoded with CurrentEncoding.
		/// </summary>
		private byte[] endBoundaryBinary;

		/// <summary>
		///     Determines if we have consumed the end boundary binary and determines
		///     if we are done parsing.
		/// </summary>
		private bool readEndBoundary;

		/// <summary>
		///     Initializes a new instance of the <see cref="StreamingBinaryMultipartFormDataParser" /> class
		///     with the boundary, stream, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. set this to true to ignore invalid parts.
		/// </param>
		public StreamingBinaryMultipartFormDataParser(Stream stream, Encoding encoding, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
			: this(stream, null, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts)
		{
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="StreamingBinaryMultipartFormDataParser" /> class
		///     with the boundary, stream, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="boundary">
		///     The multipart/form-data boundary. This should be the value
		///     returned by the request header.
		/// </param>
		/// <param name="encoding">
		///     The encoding of the multipart data.
		/// </param>
		/// <param name="binaryBufferSize">
		///     The size of the buffer to use for parsing the multipart form data. This must be larger
		///     then (size of boundary + 4 + # bytes in newline).
		/// </param>
		/// <param name="binaryMimeTypes">
		///     List of mimetypes that should be detected as file.
		/// </param>
		/// <param name="ignoreInvalidParts">
		///     By default the parser will throw an exception if it encounters an invalid part. set this to true to ignore invalid parts.
		/// </param>
		public StreamingBinaryMultipartFormDataParser(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
			: this(stream, new ParserOptions
			{
				BinaryBufferSize = binaryBufferSize,
				BinaryMimeTypes = binaryMimeTypes ?? Constants.DefaultBinaryMimeTypes,
				Boundary = boundary,
				Encoding = encoding ?? Constants.DefaultEncoding,
				IgnoreInvalidParts = ignoreInvalidParts
			})
		{
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="StreamingBinaryMultipartFormDataParser" /> class
		///     with the boundary, stream, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="options">
		///     The options that configure the parser.
		/// </param>
		public StreamingBinaryMultipartFormDataParser(Stream stream, ParserOptions options)
		{
			if (stream == null || stream == Stream.Null) throw new ArgumentNullException(nameof(stream));
			_stream = stream;

			if (options == null)
			{
				_options = new ParserOptions();
			}
			else
			{
				if (options.Encoding == null) throw new ArgumentNullException("You must specify the encoding", $"{nameof(options)}.{nameof(options.Encoding)}");
				if (options.BinaryBufferSize <= 0) throw new ArgumentNullException("You must specify a value greater than zero", $"{nameof(options)}.{nameof(options.BinaryBufferSize)}");
				if (options.BinaryMimeTypes == null || options.BinaryMimeTypes.Length == 0) throw new ArgumentException("You must specify at least one MIME type that identifies file attachments", $"{nameof(options)}.{nameof(options.BinaryMimeTypes)}");

				_options = options;
			}

			readEndBoundary = false;
		}

		/// <summary>
		///     Begins executing the parser. This should be called after all handlers have been set.
		/// </summary>
		public void Run()
		{
			var reader = new RebufferableBinaryReader(_stream, Encoding, BinaryBufferSize);

			// If we don't know the boundary now is the time to calculate it.
			boundary = $"--{_options.Boundary ?? DetectBoundary(reader)}";
			endBoundary = $"{boundary}--";

			// We add newline here because unlike reader.ReadLine() binary reading
			// does not automatically consume the newline, we want to add it to our signature
			// so we can automatically detect and consume newlines after the boundary
			boundaryBinary = Encoding.GetBytes(boundary);
			endBoundaryBinary = Encoding.GetBytes(endBoundary);

			Debug.Assert(
				BinaryBufferSize >= endBoundaryBinary.Length,
				"binaryBufferSize must be bigger then the boundary");

			Parse(reader);
		}

		/// <summary>
		///     Begins executing the parser asynchronously. This should be called after all handlers have been set.
		/// </summary>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The asynchronous task.
		/// </returns>
		public async Task RunAsync(CancellationToken cancellationToken = default)
		{
			var reader = new RebufferableBinaryReader(_stream, Encoding, BinaryBufferSize);

			// If we don't know the boundary now is the time to calculate it.
			boundary = $"--{_options.Boundary ?? await DetectBoundaryAsync(reader, cancellationToken).ConfigureAwait(false)}";
			endBoundary = $"{boundary}--";

			// We add newline here because unlike reader.ReadLine() binary reading
			// does not automatically consume the newline, we want to add it to our signature
			// so we can automatically detect and consume newlines after the boundary
			boundaryBinary = Encoding.GetBytes(boundary);
			endBoundaryBinary = Encoding.GetBytes(endBoundary);

			Debug.Assert(
				BinaryBufferSize >= endBoundaryBinary.Length,
				"binaryBufferSize must be bigger then the boundary");

			await ParseAsync(reader, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Gets the binary buffer size.
		/// </summary>
		public int BinaryBufferSize => _options.BinaryBufferSize;

		/// <summary>
		/// Gets the encoding.
		/// </summary>
		public Encoding Encoding => _options.Encoding;

		/// <summary>
		/// Gets or sets the FileHandler. Delegates attached to this property will receive sequential file stream data from this parser.
		/// </summary>
		public FileStreamDelegate FileHandler { get; set; }

		/// <summary>
		/// Gets or sets the ParameterHandler. Delegates attached to this property will receive parameter data.
		/// </summary>
		public BinaryParameterDelegate ParameterHandler { get; set; }

		/// <summary>
		/// Gets or sets the StreamClosedHandler. Delegates attached to this property will be notified when the source stream is exhausted.
		/// </summary>
		public StreamClosedDelegate StreamClosedHandler { get; set; }

		/// <summary>
		///     Detects the boundary from the input stream. Assumes that the
		///     current position of the reader is the start of the file and therefore
		///     the beginning of the boundary.
		/// </summary>
		/// <remarks>
		/// As of version 8.2.0 (released in June 2023), we ignore blank lines at the
		/// start of the stream. In previous version, an exception was thrown if any
		/// blank line was present before the boundary marker.
		/// </remarks>
		/// <param name="reader">
		///     The binary reader to parse.
		/// </param>
		/// <returns>
		///     The boundary string.
		/// </returns>
		private static string DetectBoundary(RebufferableBinaryReader reader)
		{
			// Presumably the boundary is --|||||||||||||| where -- is the stuff added on to
			// the front as per the protocol and ||||||||||||| is the part we care about.

			// The following loop ignores blank lines that may be present before the first line of the form.
			// It's highly unusual to find blank lines at the start of the data but it's a possible scenario described in GH-116.
			// Please note that we intentionally do NOT check for "string.IsNullOrEmpty(line)" because NULL does
			// not indicate a blank line. It indicates that we have reached the end of the stream.
			var line = string.Empty;
			while (line == string.Empty)
			{
				line = reader.ReadLine();
			}

			// The line must not be empty and must starts with "--".
			if (string.IsNullOrEmpty(line)) throw new MultipartParseException("Unable to determine boundary: either the stream is empty or we reached the end of the stream");
			else if (!line.StartsWith("--")) throw new MultipartParseException("Unable to determine boundary: content does not start with a valid multipart boundary");

			// Remove the two dashes
			string boundary = line.Substring(2);

			// If the string ends with '--' and it's not followed by content, we can safely assume that we
			// found the "end" boundary. In this scenario, we must trim the two dashes to get the actual boundary.
			// Otherwise, the boundary must be accepted as-is. The reason we check for "additional content" is to
			// resolve the problem explained in GH-123.
			var moreContentAvailable = MoreContentAvailable(reader);
			if (boundary.EndsWith("--") && !moreContentAvailable)
			{
				boundary = boundary.Substring(0, boundary.Length - 2);
				reader.Buffer($"--{boundary}--\n");
			}
			else
			{
				reader.Buffer($"--{boundary}\n");
			}

			return boundary;
		}

		/// <summary>
		///     Detects the boundary from the input stream. Assumes that the
		///     current position of the reader is the start of the file and therefore
		///     the beginning of the boundary.
		/// </summary>
		/// <remarks>
		/// As of version 8.2.0 (released in June 2023), we ignore blank lines at the
		/// start of the stream. In previous version, an exception was thrown if any
		/// blank line was present before the boundary marker.
		/// </remarks>
		/// <param name="reader">
		///     The binary reader to parse.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The boundary string.
		/// </returns>
		private static async Task<string> DetectBoundaryAsync(RebufferableBinaryReader reader, CancellationToken cancellationToken = default)
		{
			// Presumably the boundary is --|||||||||||||| where -- is the stuff added on to
			// the front as per the protocol and ||||||||||||| is the part we care about.

			// The following loop ignores blank lines that may be present before the first line of the form.
			// It's highly unusual to find blank lines at the start of the data but it's a possible scenario described in GH-116.
			// Please note that we intentionally do NOT check for "string.IsNullOrEmpty(line)" because NULL does
			// not indicate a blank line. It indicates that we have reached the end of the stream.
			var line = string.Empty;
			while (line == string.Empty)
			{
				line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
			}

			// The line must not be empty and must starts with "--".
			if (string.IsNullOrEmpty(line)) throw new MultipartParseException("Unable to determine boundary: either the stream is empty or we reached the end of the stream");
			else if (!line.StartsWith("--")) throw new MultipartParseException("Unable to determine boundary: content does not start with a valid multipart boundary");

			// Remove the two dashes
			string boundary = line.Substring(2);

			// If the string ends with '--' and it's not followed by content, we can safely assume that we
			// found the "end" boundary. In this scenario, we must trim the two dashes to get the actual boundary.
			// Otherwise, the boundary must be accepted as-is. The reason we check for "additional content" is to
			// resolve the problem explained in GH-123.
			var moreContentAvailable = await MoreContentAvailableAsync(reader).ConfigureAwait(false);
			if (boundary.EndsWith("--") && !moreContentAvailable)
			{
				boundary = boundary.Substring(0, boundary.Length - 2);
				reader.Buffer($"--{boundary}--\n");
			}
			else
			{
				reader.Buffer($"--{boundary}\n");
			}

			return boundary;
		}

		/// <summary>
		///     Determine if there is more content.
		/// </summary>
		/// <param name="reader">
		///     The binary reader to parse.
		/// </param>
		/// <returns>
		///     A boolean indicating whether more content is available in the binary reader.
		/// </returns>
		private static bool MoreContentAvailable(RebufferableBinaryReader reader)
		{
			var line = reader.ReadLine();

			if (line == null) return false;
			else reader.Buffer($"{line}\n");

			return true;
		}

		/// <summary>
		///     Determine if there is more content.
		/// </summary>
		/// <param name="reader">
		///     The binary reader to parse.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     A boolean indicating whether more content is available in the binary reader.
		/// </returns>
		private static async Task<bool> MoreContentAvailableAsync(RebufferableBinaryReader reader, CancellationToken cancellationToken = default)
		{
			var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

			if (line == null) return false;
			else reader.Buffer($"{line}\n");

			return true;
		}

		/// <summary>
		/// Use a few assumptions to determine if a section contains a file.
		/// </summary>
		/// <param name="parameters">The section parameters.</param>
		/// <returns>true if the section contains a file, false otherwise.</returns>
		private bool IsFilePart(IDictionary<string, string> parameters)
		{
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			// A section without any parameter is invalid. It is very likely to contain just a bunch of blank lines.
			if (parameters.Count == 0) return false;

			// If a section contains filename, then it's a file.
			else if (parameters.ContainsKey("filename") || parameters.ContainsKey("filename*")) return true;

			// Check if mimetype is a binary file
			else if (parameters.ContainsKey("content-type") && _options.BinaryMimeTypes.Contains(parameters["content-type"])) return true;

			// If the section is missing the filename and the name, then it's a file.
			// For example, images in an mjpeg stream have neither a name nor a filename.
			else if (!parameters.ContainsKey("name")) return true;

			// Otherwise this section does not contain a file.
			return false;
		}

		/// <summary>
		/// Use a few assumptions to determine if a section contains a "data" parameter.
		/// </summary>
		/// <param name="parameters">The section parameters.</param>
		/// <returns>true if the section contains a data parameter, false otherwise.</returns>
		private bool IsParameterPart(IDictionary<string, string> parameters)
		{
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			// A section without any parameter is invalid. It is very likely to contain just a bunch of blank lines.
			if (parameters.Count == 0) return false;

			// A data parameter MUST have a name.
			else if (parameters.ContainsKey("name")) return true;

			// Otherwise this section does not contain a data parameter.
			return false;
		}

		/// <summary>
		///     Finds the next sequence of newlines in the input stream.
		/// </summary>
		/// <param name="data">The data to search.</param>
		/// <param name="offset">The offset to start searching at.</param>
		/// <param name="maxBytes">The maximum number of bytes (starting from offset) to search.</param>
		/// <returns>The offset of the next newline.</returns>
		private int FindNextNewline(ref byte[] data, int offset, int maxBytes)
		{
			byte[][] newlinePatterns = { Encoding.GetBytes("\r\n"), Encoding.GetBytes("\n") };
			Array.Sort(newlinePatterns, (first, second) => second.Length.CompareTo(first.Length));

			byte[] dataRef = data;
			if (offset != 0)
			{
				dataRef = data.Skip(offset).ToArray();
			}

			foreach (var pattern in newlinePatterns)
			{
				int position = SubsequenceFinder.Search(dataRef, pattern, maxBytes);
				if (position != -1)
				{
					return position + offset;
				}
			}

			return -1;
		}

		/// <summary>
		///     Calculates the length of the next found newline.
		///     data[offset] is the start of the space to search.
		/// </summary>
		/// <param name="data">
		///     The data containing the newline.
		/// </param>
		/// <param name="offset">
		///     The offset of the start of the newline.
		/// </param>
		/// <returns>
		///     The length in bytes of the newline sequence.
		/// </returns>
		private int CalculateNewlineLength(ref byte[] data, int offset)
		{
			byte[][] newlinePatterns = { Encoding.GetBytes("\r\n"), Encoding.GetBytes("\n") };

			// Go through each pattern and find which one matches.
			foreach (var pattern in newlinePatterns)
			{
				bool found = false;
				for (int i = 0; i < pattern.Length; ++i)
				{
					int readPos = offset + i;
					if (data.Length == readPos || pattern[i] != data[readPos])
					{
						found = false;
						break;
					}

					found = true;
				}

				if (found)
				{
					return pattern.Length;
				}
			}

			return 0;
		}

		/// <summary>
		///     Begins the parsing of the stream into objects.
		/// </summary>
		/// <param name="reader">
		///     The multipart/form-data binary reader to parse from.
		/// </param>
		/// <exception cref="MultipartParseException">
		///     thrown on finding unexpected data such as a boundary before we are ready for one.
		/// </exception>
		private void Parse(RebufferableBinaryReader reader)
		{
			// Parsing references include:
			// RFC1341 section 7: http://www.w3.org/Protocols/rfc1341/7_2_Multipart.html
			// RFC2388: http://www.ietf.org/rfc/rfc2388.txt

			// First we need to read until we find a boundary
			while (true)
			{
				string line = reader.ReadLine();

				if (line == boundary)
				{
					break;
				}
				else if (line == endBoundary)
				{
					readEndBoundary = true;
					break;
				}
				else if (line == null)
				{
					throw new MultipartParseException("Could not find expected boundary");
				}
			}

			// Now that we've found the initial boundary we know where to start.
			// We need parse each individual section
			while (!readEndBoundary)
			{
				// ParseSection will parse up to and including
				// the next boundary.
				ParseSection(reader);
			}

			StreamClosedHandler?.Invoke();
		}

		/// <summary>
		///     Begins the asynchronous parsing of the stream into objects.
		/// </summary>
		/// <param name="reader">
		///     The multipart/form-data binary reader to parse from.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The asynchronous task.
		/// </returns>
		/// <exception cref="MultipartParseException">
		///     thrown on finding unexpected data such as a boundary before we are ready for one.
		/// </exception>
		private async Task ParseAsync(RebufferableBinaryReader reader, CancellationToken cancellationToken = default)
		{
			// Parsing references include:
			// RFC1341 section 7: http://www.w3.org/Protocols/rfc1341/7_2_Multipart.html
			// RFC2388: http://www.ietf.org/rfc/rfc2388.txt

			// First we need to read until we find a boundary
			while (true)
			{
				string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

				if (line == boundary)
				{
					break;
				}
				else if (line == endBoundary)
				{
					readEndBoundary = true;
					break;
				}
				else if (line == null)
				{
					throw new MultipartParseException("Could not find expected boundary");
				}
			}

			// Now that we've found the initial boundary we know where to start.
			// We need parse each individual section
			while (!readEndBoundary)
			{
				// ParseSection will parse up to and including
				// the next boundary.
				await ParseSectionAsync(reader, cancellationToken).ConfigureAwait(false);
			}

			StreamClosedHandler?.Invoke();
		}

		/// <summary>
		///     Parses a section of the stream that is known to be file data.
		/// </summary>
		/// <param name="parameters">
		///     The header parameters of this file.
		/// </param>
		/// <param name="reader">
		///     The StreamReader to read the data from.
		/// </param>
		private void ParseFilePart(Dictionary<string, string> parameters, RebufferableBinaryReader reader)
		{
			int partNumber = 0; // begins count parts of file from 0

			// Read the parameters
			parameters.TryGetValue("name", out string name);
			parameters.TryGetValue("filename", out string filename);
			parameters.TryGetValue("filename*", out string filenameStar);
			parameters.TryGetValue("content-type", out string contentType);
			parameters.TryGetValue("content-disposition", out string contentDisposition);

			// Per RFC6266 section 4.3, we should favor "filename*" over "filename"
			if (!string.IsNullOrEmpty(filenameStar)) filename = RFC5987.Decode(filenameStar);

			// Filter out the "well known" parameters.
			var additionalParameters = GetAdditionalParameters(parameters);

			// Default values if expected parameters are missing
			contentType ??= "text/plain";
			contentDisposition ??= "form-data";

			// We want to create a stream and fill it with the data from the file.
			var curBuffer = Utilities.ArrayPool.Rent(BinaryBufferSize);
			var prevBuffer = Utilities.ArrayPool.Rent(BinaryBufferSize);
			var fullBuffer = Utilities.ArrayPool.Rent(BinaryBufferSize * 2);
			int curLength;
			int prevLength;
			int fullLength;

			prevLength = reader.Read(prevBuffer, 0, BinaryBufferSize);
			do
			{
				curLength = reader.Read(curBuffer, 0, BinaryBufferSize);

				// Combine both buffers into the fullBuffer
				// See: http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
				Buffer.BlockCopy(prevBuffer, 0, fullBuffer, 0, prevLength);
				Buffer.BlockCopy(curBuffer, 0, fullBuffer, prevLength, curLength);
				fullLength = prevLength + curLength;

				// Now we want to check for a substring within the current buffer.
				// We need to find the closest substring greedily. That is find the
				// closest boundary and don't miss the end --'s if it's an end boundary.
				int endBoundaryPos = SubsequenceFinder.Search(fullBuffer, endBoundaryBinary, fullLength);
				int endBoundaryLength = endBoundaryBinary.Length;

				int boundaryPos = SubsequenceFinder.Search(fullBuffer, boundaryBinary, fullLength);
				int boundaryLength = boundaryBinary.Length;

				// If the boundaryPos is exactly at the end of our full buffer then ignore it as it could
				// actually be a endBoundary that's had one or both of the '--' chopped off by the buffer.
				if (boundaryPos + boundaryLength == fullLength ||
				   boundaryPos + boundaryLength + 1 == fullLength)
				{
					boundaryPos = -1;
				}

				// We need to select the appropriate position and length
				// based on the smallest non-negative position.
				int endPos = -1;
				int endPosLength = 0;

				if (endBoundaryPos >= 0 && boundaryPos >= 0)
				{
					if (boundaryPos < endBoundaryPos)
					{
						// Select boundary
						endPos = boundaryPos;
						endPosLength = boundaryLength;
					}
					else
					{
						// Select end boundary
						endPos = endBoundaryPos;
						endPosLength = endBoundaryLength;
						readEndBoundary = true;
					}
				}
				else if (boundaryPos >= 0 && endBoundaryPos < 0)
				{
					// Select boundary
					endPos = boundaryPos;
					endPosLength = boundaryLength;
				}
				else if (boundaryPos < 0 && endBoundaryPos >= 0)
				{
					// Select end boundary
					endPos = endBoundaryPos;
					endPosLength = endBoundaryLength;
					readEndBoundary = true;
				}

				if (endPos != -1)
				{
					// Now we need to check if the endPos is followed by \r\n or just \n. HTTP
					// specifies \r\n but some clients might encode with \n. Or we might get 0 if
					// we are at the end of the file.
					int boundaryNewlineOffset = CalculateNewlineLength(ref fullBuffer, Math.Min(fullLength - 1, endPos + endPosLength));

					// We also need to check if the last n characters of the buffer to write
					// are a newline and if they are ignore them.
					int maxNewlineBytes = Encoding.GetMaxByteCount(2);
					int bufferNewlineOffset = FindNextNewline(ref fullBuffer, Math.Max(0, endPos - maxNewlineBytes), maxNewlineBytes);
					int bufferNewlineLength = CalculateNewlineLength(ref fullBuffer, bufferNewlineOffset);

					// We've found an end. We need to consume all the binary up to it
					// and then write the remainder back to the original stream. Then we
					// need to modify the original streams position to take into account
					// the new data.
					// We also want to chop off the newline that is inserted by the protocol.
					// We can do this by reducing endPos by the length of newline in this environment
					// and encoding
					FileHandler?.Invoke(name, filename, contentType, contentDisposition, fullBuffer, endPos - bufferNewlineLength, partNumber++, additionalParameters);

					int writeBackOffset = endPos + endPosLength + boundaryNewlineOffset;
					int writeBackAmount = (prevLength + curLength) - writeBackOffset;
					reader.Buffer(fullBuffer, writeBackOffset, writeBackAmount);

					break;
				}

				// No end, consume the entire previous buffer
				FileHandler?.Invoke(name, filename, contentType, contentDisposition, prevBuffer, prevLength, partNumber++, additionalParameters);

				// Now we want to swap the two buffers, we don't care
				// what happens to the data from prevBuffer so we set
				// curBuffer to it so it gets overwritten.
				(prevBuffer, curBuffer) = (curBuffer, prevBuffer);

				// We don't need to swap the lengths because
				// curLength will be overwritten in the next
				// iteration of the loop.
				prevLength = curLength;
			}
			while (prevLength != 0);

			Utilities.ArrayPool.Return(curBuffer);
			Utilities.ArrayPool.Return(prevBuffer);
			Utilities.ArrayPool.Return(fullBuffer);
		}

		/// <summary>
		///     Asynchronously parses a section of the stream that is known to be file data.
		/// </summary>
		/// <param name="parameters">
		///     The header parameters of this file, expects "name" and "filename" to be valid keys.
		/// </param>
		/// <param name="reader">
		///     The StreamReader to read the data from.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The asynchronous task.
		/// </returns>
		private async Task ParseFilePartAsync(Dictionary<string, string> parameters, RebufferableBinaryReader reader, CancellationToken cancellationToken = default)
		{
			int partNumber = 0; // begins count parts of file from 0

			// Read the parameters
			parameters.TryGetValue("name", out string name);
			parameters.TryGetValue("filename", out string filename);
			parameters.TryGetValue("filename*", out string filenameStar);
			parameters.TryGetValue("content-type", out string contentType);
			parameters.TryGetValue("content-disposition", out string contentDisposition);

			// Per RFC6266 section 4.3, we should favor "filename*" over "filename"
			if (!string.IsNullOrEmpty(filenameStar)) filename = RFC5987.Decode(filenameStar);

			// Filter out the "well known" parameters.
			var additionalParameters = GetAdditionalParameters(parameters);

			// Default values if expected parameters are missing
			contentType ??= "text/plain";
			contentDisposition ??= "form-data";

			// We want to create a stream and fill it with the data from the
			// file.
			var curBuffer = Utilities.ArrayPool.Rent(BinaryBufferSize);
			var prevBuffer = Utilities.ArrayPool.Rent(BinaryBufferSize);
			var fullBuffer = Utilities.ArrayPool.Rent(BinaryBufferSize * 2);
			int curLength;
			int prevLength;
			int fullLength;

			prevLength = await reader.ReadAsync(prevBuffer, 0, BinaryBufferSize, cancellationToken).ConfigureAwait(false);
			do
			{
				curLength = await reader.ReadAsync(curBuffer, 0, BinaryBufferSize, cancellationToken).ConfigureAwait(false);

				// Combine both buffers into the fullBuffer
				// See: http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
				Buffer.BlockCopy(prevBuffer, 0, fullBuffer, 0, prevLength);
				Buffer.BlockCopy(curBuffer, 0, fullBuffer, prevLength, curLength);
				fullLength = prevLength + curLength;

				// Now we want to check for a substring within the current buffer.
				// We need to find the closest substring greedily. That is find the
				// closest boundary and don't miss the end --'s if it's an end boundary.
				int endBoundaryPos = SubsequenceFinder.Search(fullBuffer, endBoundaryBinary, fullLength);
				int endBoundaryLength = endBoundaryBinary.Length;

				int boundaryPos = SubsequenceFinder.Search(fullBuffer, boundaryBinary, fullLength);
				int boundaryLength = boundaryBinary.Length;

				// If the boundaryPos is exactly at the end of our full buffer then ignore it as it could
				// actually be a endBoundary that's had one or both of the '--' chopped off by the buffer.
				if (boundaryPos + boundaryLength == fullLength ||
				   boundaryPos + boundaryLength + 1 == fullLength)
				{
					boundaryPos = -1;
				}

				// We need to select the appropriate position and length
				// based on the smallest non-negative position.
				int endPos = -1;
				int endPosLength = 0;

				if (endBoundaryPos >= 0 && boundaryPos >= 0)
				{
					if (boundaryPos < endBoundaryPos)
					{
						// Select boundary
						endPos = boundaryPos;
						endPosLength = boundaryLength;
					}
					else
					{
						// Select end boundary
						endPos = endBoundaryPos;
						endPosLength = endBoundaryLength;
						readEndBoundary = true;
					}
				}
				else if (boundaryPos >= 0 && endBoundaryPos < 0)
				{
					// Select boundary
					endPos = boundaryPos;
					endPosLength = boundaryLength;
				}
				else if (boundaryPos < 0 && endBoundaryPos >= 0)
				{
					// Select end boundary
					endPos = endBoundaryPos;
					endPosLength = endBoundaryLength;
					readEndBoundary = true;
				}

				if (endPos != -1)
				{
					// Now we need to check if the endPos is followed by \r\n or just \n. HTTP
					// specifies \r\n but some clients might encode with \n. Or we might get 0 if
					// we are at the end of the file.
					int boundaryNewlineOffset = CalculateNewlineLength(ref fullBuffer, Math.Min(fullLength - 1, endPos + endPosLength));

					// We also need to check if the last n characters of the buffer to write
					// are a newline and if they are ignore them.
					int maxNewlineBytes = Encoding.GetMaxByteCount(2);
					int bufferNewlineOffset = FindNextNewline(ref fullBuffer, Math.Max(0, endPos - maxNewlineBytes), maxNewlineBytes);
					int bufferNewlineLength = CalculateNewlineLength(ref fullBuffer, bufferNewlineOffset);

					// We've found an end. We need to consume all the binary up to it
					// and then write the remainder back to the original stream. Then we
					// need to modify the original streams position to take into account
					// the new data.
					// We also want to chop off the newline that is inserted by the protocl.
					// We can do this by reducing endPos by the length of newline in this environment
					// and encoding
					FileHandler?.Invoke(name, filename, contentType, contentDisposition, fullBuffer, endPos - bufferNewlineLength, partNumber++, additionalParameters);

					int writeBackOffset = endPos + endPosLength + boundaryNewlineOffset;
					int writeBackAmount = (prevLength + curLength) - writeBackOffset;
					reader.Buffer(fullBuffer, writeBackOffset, writeBackAmount);

					break;
				}

				// No end, consume the entire previous buffer
				FileHandler?.Invoke(name, filename, contentType, contentDisposition, prevBuffer, prevLength, partNumber++, additionalParameters);

				// Now we want to swap the two buffers, we don't care
				// what happens to the data from prevBuffer so we set
				// curBuffer to it so it gets overwritten.
				(prevBuffer, curBuffer) = (curBuffer, prevBuffer);

				// We don't need to swap the lengths because
				// curLength will be overwritten in the next
				// iteration of the loop.
				prevLength = curLength;
			}
			while (prevLength != 0);

			Utilities.ArrayPool.Return(curBuffer);
			Utilities.ArrayPool.Return(prevBuffer);
			Utilities.ArrayPool.Return(fullBuffer);
		}

		/// <summary>
		///     Parses a section of the stream that is known to be parameter data.
		/// </summary>
		/// <param name="parameters">
		///     The header parameters of this section. "name" must be a valid key.
		/// </param>
		/// <param name="reader">
		///     The StreamReader to read the data from.
		/// </param>
		/// <exception cref="MultipartParseException">
		///     thrown if unexpected data is found such as running out of stream before hitting the boundary.
		/// </exception>
		private void ParseParameterPart(Dictionary<string, string> parameters, RebufferableBinaryReader reader)
		{
			var data = new List<byte[]>();
			var line = reader.ReadByteLine();
			while (!line.SequenceEqual(boundaryBinary) && !line.SequenceEqual(endBoundaryBinary))
			{
				if (line == null) throw new MultipartParseException("Unexpected end of stream. Is there an end boundary?");
				data.Add(line);
				line = reader.ReadByteLine();
			}

			if (line.SequenceEqual(endBoundaryBinary)) readEndBoundary = true;

			var part = new ParameterPartBinary(parameters["name"], data);
			ParameterHandler?.Invoke(part);
		}

		/// <summary>
		///     Asynchronously parses a section of the stream that is known to be parameter data.
		/// </summary>
		/// <param name="parameters">
		///     The header parameters of this section. "name" must be a valid key.
		/// </param>
		/// <param name="reader">
		///     The StreamReader to read the data from.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The asynchronous task.
		/// </returns>
		/// <exception cref="MultipartParseException">
		///     thrown if unexpected data is found such as running out of stream before hitting the boundary.
		/// </exception>
		private async Task ParseParameterPartAsync(Dictionary<string, string> parameters, RebufferableBinaryReader reader, CancellationToken cancellationToken = default)
		{
			var data = new List<byte[]>();
			var line = await reader.ReadByteLineAsync().ConfigureAwait(false);
			while (!line.SequenceEqual(boundaryBinary) && !line.SequenceEqual(endBoundaryBinary))
			{
				if (line == null) throw new MultipartParseException("Unexpected end of stream. Is there an end boundary?");
				data.Add(line);
				line = await reader.ReadByteLineAsync(cancellationToken).ConfigureAwait(false);
			}

			if (line.SequenceEqual(endBoundaryBinary)) readEndBoundary = true;

			var part = new ParameterPartBinary(parameters["name"], data);
			ParameterHandler?.Invoke(part);
		}

		/// <summary>
		///     Skip a section of the stream.
		///     This is used when a section is deemed to be invalid and the developer has requested to ignore invalid parts.
		/// </summary>
		/// <param name="reader">
		///     The StreamReader to read the data from.
		/// </param>
		/// <exception cref="MultipartParseException">
		///     thrown if unexpected data is found such as running out of stream before hitting the boundary.
		/// </exception>
		private void SkipPart(RebufferableBinaryReader reader)
		{
			// Our job is to consume the lines in this section and discard them
			var line = reader.ReadByteLine();
			while (!line.SequenceEqual(boundaryBinary) && !line.SequenceEqual(endBoundaryBinary))
			{
				if (line == null) throw new MultipartParseException("Unexpected end of stream. Is there an end boundary?");
				line = reader.ReadByteLine();
			}

			if (line.SequenceEqual(endBoundaryBinary)) readEndBoundary = true;
		}

		/// <summary>
		///     Asynchronously skip a section of the stream.
		///     This is used when a section is deemed to be invalid and the developer has requested to ignore invalid parts.
		/// </summary>
		/// <param name="reader">
		///     The StreamReader to read the data from.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The asynchronous task.
		/// </returns>
		/// <exception cref="MultipartParseException">
		///     thrown if unexpected data is found such as running out of stream before hitting the boundary.
		/// </exception>
		private async Task SkipPartAsync(RebufferableBinaryReader reader, CancellationToken cancellationToken = default)
		{
			// Our job is to consume the lines in this section and discard them
			var line = await reader.ReadByteLineAsync(cancellationToken).ConfigureAwait(false);
			while (!line.SequenceEqual(boundaryBinary) && !line.SequenceEqual(endBoundaryBinary))
			{
				if (line == null) throw new MultipartParseException("Unexpected end of stream. Is there an end boundary?");
				line = await reader.ReadByteLineAsync(cancellationToken).ConfigureAwait(false);
			}

			if (line.SequenceEqual(endBoundaryBinary)) readEndBoundary = true;
		}

		/// <summary>
		///     Parses the header of the next section of the multipart stream and
		///     determines if it contains file data or parameter data.
		/// </summary>
		/// <param name="reader">
		///     The StreamReader to read data from.
		/// </param>
		/// <exception cref="MultipartParseException">
		///     thrown if unexpected data is hit such as end of stream.
		/// </exception>
		private void ParseSection(RebufferableBinaryReader reader)
		{
			// Our first job is to determine what type of section this is: form data or file.
			// This is a bit tricky because files can still be encoded with Content-Disposition: form-data
			// in the case of single file uploads. Multi-file uploads have Content-Disposition: file according
			// to the spec however in practise it seems that multiple files will be represented by
			// multiple Content-Disposition: form-data files.
			var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			string line = reader.ReadLine();
			while (line != string.Empty)
			{
				if (line == null)
				{
					throw new MultipartParseException("Unexpected end of stream");
				}

				if (line == boundary || line == endBoundary)
				{
					throw new MultipartParseException("Unexpected end of section");
				}

				// This line parses the header values into a set of key/value pairs.
				// For example:
				//   Content-Disposition: form-data; name="textdata"
				//     ["content-disposition"] = "form-data"
				//     ["name"] = "textdata"
				//   Content-Disposition: form-data; name="file"; filename="data.txt"
				//     ["content-disposition"] = "form-data"
				//     ["name"] = "file"
				//     ["filename"] = "data.txt"
				//   Content-Type: text/plain
				//     ["content-type"] = "text/plain"
				Dictionary<string, string> values = SplitBySemicolonIgnoringSemicolonsInQuotes(line)
					.Select(x => x.Split(new[] { ':', '=' }, 2))

					// select where the length of the array is equal to two, that way if it is only one it will
					// be ignored as it is invalid key-pair
					.Where(x => x.Length == 2)

					// Limit split to 2 splits so we don't accidently split characters in file paths.
					.ToDictionary(
						x => x[0].Trim().Replace("\"", string.Empty),
						x => x[1].Trim().Replace("\"", string.Empty),
						StringComparer.OrdinalIgnoreCase);

				// Here we just want to push all the values that we just retrieved into the
				// parameters dictionary.
				try
				{
					foreach (var pair in values)
					{
						parameters.Add(pair.Key, pair.Value);
					}
				}
				catch (ArgumentException)
				{
					throw new MultipartParseException("Duplicate field in section");
				}

				line = reader.ReadLine();
			}

			// Now that we've consumed all the parameters we're up to the body. We're going to do
			// different things depending on if we're parsing a, relatively small, form value or a
			// potentially large file.
			if (IsFilePart(parameters))
			{
				ParseFilePart(parameters, reader);
			}
			else if (IsParameterPart(parameters))
			{
				ParseParameterPart(parameters, reader);
			}
			else if (_options.IgnoreInvalidParts)
			{
				SkipPart(reader);
			}
			else
			{
				throw new MultipartParseException("Unable to determine the section type. Some possible reasons include: section is malformed, required parameters such as 'name', 'content-type' or 'filename' are missing, section contains nothing but empty lines.");
			}
		}

		/// <summary>
		///     Asynchronously parses the header of the next section of the multipart stream and
		///     determines if it contains file data or parameter data.
		/// </summary>
		/// <param name="reader">
		///     The StreamReader to read data from.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     The asynchronous task.
		/// </returns>
		/// <exception cref="MultipartParseException">
		///     thrown if unexpected data is hit such as end of stream.
		/// </exception>
		private async Task ParseSectionAsync(RebufferableBinaryReader reader, CancellationToken cancellationToken)
		{
			// Our first job is to determine what type of section this is: form data or file.
			// This is a bit tricky because files can still be encoded with Content-Disposition: form-data
			// in the case of single file uploads. Multi-file uploads have Content-Disposition: file according
			// to the spec however in practise it seems that multiple files will be represented by
			// multiple Content-Disposition: form-data files.
			var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
			while (line != string.Empty)
			{
				if (line == null)
				{
					throw new MultipartParseException("Unexpected end of stream");
				}

				if (line == boundary || line == endBoundary)
				{
					throw new MultipartParseException("Unexpected end of section");
				}

				// This line parses the header values into a set of key/value pairs.
				// For example:
				//   Content-Disposition: form-data; name="textdata"
				//     ["content-disposition"] = "form-data"
				//     ["name"] = "textdata"
				//   Content-Disposition: form-data; name="file"; filename="data.txt"
				//     ["content-disposition"] = "form-data"
				//     ["name"] = "file"
				//     ["filename"] = "data.txt"
				//   Content-Type: text/plain
				//     ["content-type"] = "text/plain"
				Dictionary<string, string> values = SplitBySemicolonIgnoringSemicolonsInQuotes(line)
					.Select(x => x.Split(new[] { ':', '=' }, 2))

					// select where the length of the array is equal to two, that way if it is only one it will
					// be ignored as it is invalid key-pair
					.Where(x => x.Length == 2)

					// Limit split to 2 splits so we don't accidently split characters in file paths.
					.ToDictionary(
						x => x[0].Trim().Replace("\"", string.Empty),
						x => x[1].Trim().Replace("\"", string.Empty),
						StringComparer.OrdinalIgnoreCase);

				// Here we just want to push all the values that we just retrieved into the
				// parameters dictionary.
				try
				{
					foreach (var pair in values)
					{
						parameters.Add(pair.Key, pair.Value);
					}
				}
				catch (ArgumentException)
				{
					throw new MultipartParseException("Duplicate field in section");
				}

				line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
			}

			// Now that we've consumed all the parameters we're up to the body. We're going to do
			// different things depending on if we're parsing a, relatively small, form value or a
			// potentially large file.
			if (IsFilePart(parameters))
			{
				await ParseFilePartAsync(parameters, reader, cancellationToken).ConfigureAwait(false);
			}
			else if (IsParameterPart(parameters))
			{
				await ParseParameterPartAsync(parameters, reader, cancellationToken).ConfigureAwait(false);
			}
			else if (_options.IgnoreInvalidParts)
			{
				await SkipPartAsync(reader).ConfigureAwait(false);
			}
			else
			{
				throw new MultipartParseException("Unable to determine the section type. Some possible reasons include: section is malformed, required parameters such as 'name', 'content-type' or 'filename' are missing, section contains nothing but empty lines.");
			}
		}

		/// <summary>
		///     Splits a line by semicolons but ignores semicolons in quotes.
		/// </summary>
		/// <param name="line">The line to split.</param>
		/// <returns>The split strings.</returns>
		private IEnumerable<string> SplitBySemicolonIgnoringSemicolonsInQuotes(string line)
		{
			// Loop over the line looking for a semicolon. Keep track of if we're currently inside quotes
			// and if we are don't treat a semicolon as a splitting character.
			bool inQuotes = false;
			string workingString = string.Empty;

			foreach (char c in line)
			{
				if (c == '"')
				{
					inQuotes = !inQuotes;
				}

				if (c == ';' && !inQuotes)
				{
					yield return workingString;
					workingString = string.Empty;
				}
				else
				{
					workingString += c;
				}
			}

			yield return workingString;
		}

		/// <summary>
		/// Check if there are parameters other than the "well known" parameters associated with this file.
		/// This is quite rare but, as an example, the Alexa service includes a "content-ID" parameter with each file.
		/// </summary>
		/// <returns>A dictionary of parameters.</returns>
		private IDictionary<string, string> GetAdditionalParameters(IDictionary<string, string> parameters)
		{
			var wellKnownParameters = new[] { "name", "filename", "filename*", "content-type", "content-disposition" };
			var additionalParameters = parameters
				.Where(param => !wellKnownParameters.Contains(param.Key, StringComparer.OrdinalIgnoreCase))
				.ToDictionary(
					x => x.Key,
					x => x.Value,
					StringComparer.OrdinalIgnoreCase);
			return additionalParameters;
		}
	}
}
