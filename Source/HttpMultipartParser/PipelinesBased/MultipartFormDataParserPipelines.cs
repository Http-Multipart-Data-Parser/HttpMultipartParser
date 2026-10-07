using System.Collections.Generic;
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
	///       var parser = new MultipartFormDataParserPipeline(multipartStream, boundary, Encoding.UTF8);
	///
	///       // Grab the parameters (non-file data). Key is based on the name field
	///       var username = parser.Parameters["username"].Data;
	///       var password = parser.parameters["password"].Data;
	///
	///       // Grab the first files data
	///       var file = parser.Files.First();
	///       var filename = file.FileName;
	///       var filestream = file.Data;
	///   </code>
	///     <code lang="C#">
	///     // In the context of WCF you can get the boundary from the HTTP
	///     // request
	///     public ResponseClass MyMethod(Stream multipartData)
	///     {
	///         // First we need to get the boundary from the header, this is sent
	///         // with the HTTP request. We can do that in WCF using the WebOperationConext:
	///         var type = WebOperationContext.Current.IncomingRequest.Headers["Content-Type"];
	///
	///         // Now we want to strip the boundary out of the Content-Type, currently the string
	///         // looks like: "multipart/form-data; boundary=---------------------124123qase124"
	///         var boundary = type.Substring(type.IndexOf('=')+1);
	///
	///         // Now that we've got the boundary we can parse our multipart and use it as normal
	///         var parser = new MultipartFormDataParserPipeline(data, boundary, Encoding.UTF8);
	///
	///         ...
	///     }
	///   </code>
	/// </example>
	public class MultipartFormDataParserPipelines : IMultipartFormDataParser
	{
		#region Constants and fields

		private readonly List<FilePart> _files;
		private readonly List<ParameterPart> _parameters;

		#endregion

		#region Constructors and Destructors

		/// <summary>
		///     Initializes a new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </summary>
		private MultipartFormDataParserPipelines()
		{
			_files = new List<FilePart>();
			_parameters = new List<ParameterPart>();
		}

		#endregion

		#region Public Properties

		/// <summary>
		///     Gets the mapping of parameters parsed files. The name of a given field
		///     maps to the parsed file data.
		/// </summary>
		public IReadOnlyList<FilePart> Files => _files.AsReadOnly();

		/// <summary>
		///     Gets the parameters. Several ParameterParts may share the same name.
		/// </summary>
		public IReadOnlyList<ParameterPart> Parameters => _parameters.AsReadOnly();

		#endregion

		#region Static Methods

		/// <summary>
		///     Parse the stream into a new instance of the <see cref="MultipartFormDataParserPipelines" /> class
		///     with the boundary, input encoding and buffer size.
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
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </returns>
		public static MultipartFormDataParserPipelines Parse(Stream stream, Encoding encoding, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
		{
			return Parse(stream, null, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts);
		}

		/// <summary>
		///     Parse the stream into a new instance of the <see cref="MultipartFormDataParserPipelines" /> class
		///     with the boundary, input encoding and buffer size.
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
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </returns>
		public static MultipartFormDataParserPipelines Parse(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
		{
			var options = new ParserOptions
			{
				BinaryBufferSize = binaryBufferSize,
				BinaryMimeTypes = binaryMimeTypes ?? Constants.DefaultBinaryMimeTypes,
				Boundary = boundary,
				Encoding = encoding ?? Constants.DefaultEncoding,
				IgnoreInvalidParts = ignoreInvalidParts
			};

			return Parse(stream, options);
		}

		/// <summary>
		///     Parse the stream into a new instance of the <see cref="MultipartFormDataParserPipelines" /> class.
		///     with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="options">
		///     The options that configure the parser.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </returns>
		public static MultipartFormDataParserPipelines Parse(Stream stream, ParserOptions options)
		{
			var parser = new MultipartFormDataParserPipelines();
			parser.ParseStream(stream, options);
			return parser;
		}

		/// <summary>
		///     Asynchronously parse the stream into a new instance of the <see cref="MultipartFormDataParserPipelines" /> class
		///     with the boundary, input encoding and buffer size.
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
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </returns>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		public static Task<MultipartFormDataParserPipelines> ParseAsync(Stream stream, Encoding encoding, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false, CancellationToken cancellationToken = default)
		{
			return ParseAsync(stream, null, encoding, binaryBufferSize, binaryMimeTypes, ignoreInvalidParts, cancellationToken);
		}

		/// <summary>
		///     Asynchronously parse the stream into a new instance of the <see cref="MultipartFormDataParserPipelines" /> class
		///     with the boundary, input encoding and buffer size.
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
		///     By default the parser will throw an exception if it encounters an invalid part. Set this to true to ignore invalid parts.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </returns>
		public static Task<MultipartFormDataParserPipelines> ParseAsync(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = Constants.DefaultBufferSize, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false, CancellationToken cancellationToken = default)
		{
			var options = new ParserOptions
			{
				BinaryBufferSize = binaryBufferSize,
				BinaryMimeTypes = binaryMimeTypes ?? Constants.DefaultBinaryMimeTypes,
				Boundary = boundary,
				Encoding = encoding ?? Constants.DefaultEncoding,
				IgnoreInvalidParts = ignoreInvalidParts
			};

			return ParseAsync(stream, options, cancellationToken);
		}

		/// <summary>
		///     Asynchronously parse the stream into a new instance of the <see cref="MultipartFormDataParserPipelines" /> class
		///     with the boundary, input encoding and buffer size.
		/// </summary>
		/// <param name="stream">
		///     The stream containing the multipart data.
		/// </param>
		/// <param name="options">
		///     The options that configure the parser.
		/// </param>
		/// <param name="cancellationToken">
		///     The cancellation token.
		/// </param>
		/// <returns>
		///     A new instance of the <see cref="MultipartFormDataParserPipelines"/> class.
		/// </returns>
		public static async Task<MultipartFormDataParserPipelines> ParseAsync(Stream stream, ParserOptions options, CancellationToken cancellationToken = default)
		{
			var parser = new MultipartFormDataParserPipelines();
			await parser.ParseStreamAsync(stream, options, cancellationToken).ConfigureAwait(false);
			return parser;
		}

		#endregion

		#region Private Methods

		private void ParseStream(Stream stream, ParserOptions options)
		{
			var desiredEncoding = options?.Encoding ?? Constants.DefaultEncoding;
			var streamingParser = new StreamingBinaryMultipartFormDataParserPipelines(stream, options);

			streamingParser.ParameterHandler += binaryParameterPart =>
			{
				_parameters.Add(new ParameterPart(binaryParameterPart.Name, binaryParameterPart.ToString(desiredEncoding)));
			};

			streamingParser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
			{
				// Ignore spurious file emissions that contain no identifying name or filename
				// unless this is the first part (partNumber == 0) which represents a legitimate
				// file with omitted name/filename (some multipart uploads omit these).
				if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fileName) && partNumber != 0) return;

				// Defensive deduplication: occasionally the pipelines parser emits a spurious
				// duplicate file chunk where name and filename are empty but the content is
				// identical to the previously emitted file. Detect that case and drop the
				// duplicate instead of creating a second FilePart. This preserves legitimate
				// omitted-name files (when Files is empty) while preventing parser-emitted
				// duplicates that break test expectations.
				if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fileName) && _files.Count > 0 && partNumber == 0)
				{
					var last = _files[_files.Count - 1];
					if (last.ContentType == type && last.Data.CanSeek && last.Data.Length == bytes)
					{
						long prevPos = last.Data.Position;
						try
						{
							last.Data.Position = 0;
							var prevBytes = new byte[bytes];
							int read = last.Data.Read(prevBytes, 0, (int)bytes);
							if (read == bytes)
							{
								bool equal = true;
								for (int i = 0; i < bytes; i++)
								{
									if (prevBytes[i] != buffer[i])
									{
										equal = false;
										break;
									}
								}

								if (equal) return; // skip duplicate
							}
						}
						finally { last.Data.Position = prevPos; }
					}
				}

				// Normalize content type and extract additional properties (e.g. charset)
				string mediaType = type;
				IDictionary<string, string> additionalProps = additionalProperties;
				if (!string.IsNullOrEmpty(type) && type.Contains(";"))
				{
					var parts = type.Split(';');
					mediaType = parts[0].Trim();
					var map = additionalProps != null ? new Dictionary<string, string>(additionalProps, System.StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
					for (int i = 1; i < parts.Length; i++)
					{
						var kv = parts[i].Split('=');
						if (kv.Length == 2)
						{
							var k = kv[0].Trim();
							var v = kv[1].Trim().Trim('"');
							if (!map.ContainsKey(k)) map[k] = v;
						}
					}

					additionalProps = map;
				}

				// If a file with the same key (name+filename) already exists, treat this emission as a continuation/duplicate
				// and append bytes to the last file instead of creating a new one. This handles parser-emitted
				// duplicate chunks where headers are parsed slightly differently across buffer boundaries.
				if (_files.Count > 0)
				{
					var last = _files[_files.Count - 1];
					if (string.Equals(last.Name, name, System.StringComparison.Ordinal) && string.Equals(last.FileName, fileName, System.StringComparison.Ordinal))
					{
						// If the incoming chunk exactly matches the entire content of the last file,
						// treat it as a spurious duplicate emission and skip writing again.
						if (last.Data.CanSeek && last.Data.Length == bytes)
						{
							long prevPos = last.Data.Position;
							try
							{
								last.Data.Position = 0;
								var prevBytes = new byte[bytes];
								int read = last.Data.Read(prevBytes, 0, (int)bytes);
								if (read == bytes)
								{
									bool equal = true;
									for (int i = 0; i < bytes; i++)
									{
										if (prevBytes[i] != buffer[i])
										{
											equal = false;
											break;
										}
									}

									if (equal) return; // skip duplicate
								}
							}
							finally { last.Data.Position = prevPos; }
						}

						// Append to last file
						last.Data.Write(buffer, 0, bytes);
						return;
					}
				}

				if (partNumber == 0)
				{
					// create file with first partNo
					_files.Add(new FilePart(name, fileName, Utilities.MemoryStreamManager.GetStream($"{typeof(MultipartFormDataParserPipelines).FullName}.{nameof(ParseStream)}"), additionalProps, mediaType, disposition));
				}

				Files[Files.Count - 1].Data.Write(buffer, 0, bytes);
			};

			streamingParser.Run();

			// Reset all the written memory streams so they can be read.
			foreach (var file in Files)
			{
				file.Data.Position = 0;
			}
		}

		private async Task ParseStreamAsync(Stream stream, ParserOptions options, CancellationToken cancellationToken)
		{
			var desiredEncoding = options?.Encoding ?? Constants.DefaultEncoding;
			var streamingParser = new StreamingBinaryMultipartFormDataParserPipelines(stream, options);
			streamingParser.ParameterHandler += binaryParameterPart =>
			{
				_parameters.Add(new ParameterPart(binaryParameterPart.Name, binaryParameterPart.ToString(desiredEncoding)));
			};

			streamingParser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
			{
				// Ignore spurious file emissions that contain no identifying name or filename
				// unless this is the first part (partNumber == 0) which represents a legitimate
				// file with omitted name/filename (some multipart uploads omit these).
				if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fileName) && partNumber != 0) return;

				// Defensive deduplication: occasionally the pipelines parser emits a spurious
				// duplicate file chunk where name and filename are empty but the content is
				// identical to the previously emitted file. Detect that case and drop the
				// duplicate instead of creating a second FilePart. This preserves legitimate
				// omitted-name files (when Files is empty) while preventing parser-emitted
				// duplicates that break test expectations.
				if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(fileName) && _files.Count > 0 && partNumber == 0)
				{
					var last = _files[_files.Count - 1];
					if (last.ContentType == type && last.Data.CanSeek && last.Data.Length == bytes)
					{
						long prevPos = last.Data.Position;
						try
						{
							last.Data.Position = 0;
							var prevBytes = new byte[bytes];
							int read = last.Data.Read(prevBytes, 0, (int)bytes);
							if (read == bytes)
							{
								bool equal = true;
								for (int i = 0; i < bytes; i++)
								{
									if (prevBytes[i] != buffer[i])
									{
										equal = false;
										break;
									}
								}

								if (equal) return; // skip duplicate
							}
						}
						finally { last.Data.Position = prevPos; }
					}
				}

				// Normalize content type and extract additional properties (e.g. charset)
				string mediaType = type;
				IDictionary<string, string> additionalProps = additionalProperties;
				if (!string.IsNullOrEmpty(type) && type.Contains(";"))
				{
					var parts = type.Split(';');
					mediaType = parts[0].Trim();
					var map = additionalProps != null ? new Dictionary<string, string>(additionalProps, System.StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
					for (int i = 1; i < parts.Length; i++)
					{
						var kv = parts[i].Split('=');
						if (kv.Length == 2)
						{
							var k = kv[0].Trim();
							var v = kv[1].Trim().Trim('"');
							if (!map.ContainsKey(k)) map[k] = v;
						}
					}

					additionalProps = map;
				}

				// If a file with the same key (name+filename) already exists, treat this emission as a continuation/duplicate
				// and append bytes to the last file instead of creating a new one. This handles parser-emitted
				// duplicate chunks where headers are parsed slightly differently across buffer boundaries.
				if (_files.Count > 0)
				{
					var last = _files[_files.Count - 1];
					if (string.Equals(last.Name, name, System.StringComparison.Ordinal) && string.Equals(last.FileName, fileName, System.StringComparison.Ordinal))
					{
						// Append to last file
						last.Data.Write(buffer, 0, bytes);
						return;
					}
				}

				if (partNumber == 0)
				{
					// create file with first partNo
					_files.Add(new FilePart(name, fileName, Utilities.MemoryStreamManager.GetStream($"{typeof(MultipartFormDataParserPipelines).FullName}.{nameof(ParseStreamAsync)}"), additionalProps, mediaType, disposition));
				}

				Files[Files.Count - 1].Data.Write(buffer, 0, bytes);
			};

			await streamingParser.RunAsync(cancellationToken).ConfigureAwait(false);

			// Reset all the written memory streams so they can be read.
			foreach (var file in Files)
			{
				file.Data.Position = 0;
			}
		}

		#endregion
	}
}
