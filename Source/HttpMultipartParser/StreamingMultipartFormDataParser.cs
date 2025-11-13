using System.IO;
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
	public class StreamingMultipartFormDataParser : IStreamingMultipartFormDataParser
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
		///     Initializes a new instance of the <see cref="StreamingMultipartFormDataParser" /> class
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
		public StreamingMultipartFormDataParser(Stream stream, Encoding encoding, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
			: this(stream, null, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts)
		{
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="StreamingMultipartFormDataParser" /> class
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
		public StreamingMultipartFormDataParser(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
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
		///     Initializes a new instance of the <see cref="StreamingMultipartFormDataParser" /> class
		///     with the boundary, stream, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="options">
		///     The options that configure the parser.
		/// </param>
		public StreamingMultipartFormDataParser(Stream stream, ParserOptions options)
		{
			_stream = stream;
			_options = options;
		}

		/// <summary>
		///     Begins executing the parser. This should be called after all handlers have been set.
		/// </summary>
		public void Run()
		{
			var streamingParser = new StreamingBinaryMultipartFormDataParser(_stream, _options);
			streamingParser.ParameterHandler += binaryParameterPart =>
			{
				ParameterHandler?.Invoke(new ParameterPart(binaryParameterPart.Name, binaryParameterPart.ToString(Encoding)));
			};

			streamingParser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
			{
				FileHandler?.Invoke(name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties);
			};

			streamingParser.Run();
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
			var streamingParser = new StreamingBinaryMultipartFormDataParser(_stream, _options);
			streamingParser.ParameterHandler += binaryParameterPart =>
			{
				ParameterHandler?.Invoke(new ParameterPart(binaryParameterPart.Name, binaryParameterPart.ToString(Encoding)));
			};

			streamingParser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
			{
				FileHandler?.Invoke(name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties);
			};

			await streamingParser.RunAsync().ConfigureAwait(false);
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
		public ParameterDelegate ParameterHandler { get; set; }

		/// <summary>
		/// Gets or sets the StreamClosedHandler. Delegates attached to this property will be notified when the source stream is exhausted.
		/// </summary>
		public StreamClosedDelegate StreamClosedHandler { get; set; }
	}
}
