#if NET8_0_OR_GREATER
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpMultipartParser
{
	/// <summary>
	/// Pipeline-native sequence-based streaming multipart parser.
	/// Implements pooled temporary header buffers and exact-sized allocations for parameters/files.
	/// </summary>
	public class StreamingBinaryMultipartFormDataParserPipelines : IStreamingBinaryMultipartFormDataParser
	{
		private readonly Stream _stream;
		private readonly ParserOptions _options;

		// Track last emitted file to support partNumber sequencing when the parser emits
		// multiple chunks for the same logical file.
		private string _lastFileKey;
		private int _lastPartNumber;
		private bool _lastWasFile;

		/// <summary>Gets or sets the handler invoked when a file part is parsed.</summary>
		/// <remarks>The handler receives the part's name, filename, content type, content disposition, the byte array containing the file data, the length of the data, the part number (0 for first chunk, 1 for second chunk, etc.), and an optional exception if an error occurred.</remarks>
		public FileStreamDelegate FileHandler { get; set; }

		/// <summary>Gets or sets the handler invoked when a parameter part is parsed.</summary>
		public BinaryParameterDelegate ParameterHandler { get; set; }

		/// <summary>Gets or sets the handler invoked when the stream is closed.</summary>
		public StreamClosedDelegate StreamClosedHandler { get; set; }

		/// <summary>Initializes a new instance of the <see cref="StreamingBinaryMultipartFormDataParserPipelines"/> class with the specified stream and parser options.</summary>
		/// <param name="stream">The stream to parse.</param>
		/// <param name="options">The parser options.</param>
		/// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
		public StreamingBinaryMultipartFormDataParserPipelines(Stream stream, ParserOptions options)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_options = options ?? new ParserOptions();
		}

		/// <summary>Initializes a new instance of the <see cref="StreamingBinaryMultipartFormDataParserPipelines"/> class with the specified stream, boundary, encoding, binary buffer size, binary MIME types, and ignore invalid parts flag.</summary>
		/// <param name="stream">The stream to parse.</param>
		/// <param name="boundary">The boundary string.</param>
		/// <param name="encoding">The text encoding.</param>
		/// <param name="binaryBufferSize">The binary buffer size.</param>
		/// <param name="binaryMimeTypes">The binary MIME types.</param>
		/// <param name="ignoreInvalidParts">Whether to ignore invalid parts.</param>
		public StreamingBinaryMultipartFormDataParserPipelines(Stream stream, string boundary = null, Encoding encoding = null, int binaryBufferSize = 4096, string[] binaryMimeTypes = null, bool ignoreInvalidParts = false)
			: this(stream, new ParserOptions
			{
				Boundary = boundary,
				Encoding = encoding ?? Encoding.UTF8,
				BinaryBufferSize = binaryBufferSize,
				BinaryMimeTypes = binaryMimeTypes ?? new[] { "application/octet-stream" },
				IgnoreInvalidParts = ignoreInvalidParts
			})
		{ }

		/// <summary>Runs the parser synchronously. This method blocks until parsing is complete.</summary>
		public void Run() => RunAsync().GetAwaiter().GetResult();

		/// <summary>Runs the parser asynchronously. This method returns a task that completes when parsing is complete.</summary>
		/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		public async Task RunAsync(CancellationToken cancellationToken = default)
		{
			var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
			var encoding = _options?.Encoding ?? Encoding.UTF8;
			var firstBoundaryFound = false;

			if (string.IsNullOrEmpty(_options.Boundary))
			{
				var detected = await DetectBoundaryAsync(reader, encoding, cancellationToken).ConfigureAwait(false);
				if (string.IsNullOrEmpty(detected))
				{
					await reader.CompleteAsync().ConfigureAwait(false);
					return;
				}

				_options.Boundary = detected;
				firstBoundaryFound = true; // DetectBoundaryAsync consumes the initial boundary line
			}

			var boundary = "--" + _options.Boundary;
			var boundaryBytes = encoding.GetBytes(boundary);
			var headerDelimiter = encoding.GetBytes("\r\n\r\n");

			// small reusable buffers to avoid stackalloc inside the main parse loop
			var twoByteBuffer = new byte[2];
			var lastTwoBuffer = new byte[2];
			var lastOneBuffer = new byte[1];

			// Do not store ReadOnlySpan<byte> in locals that cross await boundaries; pass byte[] directly to SequenceReader.TryReadTo

			// no cross-ReadAsync SequencePosition is stored; we'll use buffer.Start as the logical parse start for each read

			// helper to detect initial boundary
			static async Task<string> DetectBoundaryAsync(PipeReader reader, Encoding encoding, CancellationToken cancellationToken)
			{
				var newline = encoding.GetBytes("\n");
				while (true)
				{
					var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
					var buffer = result.Buffer;
					var seqReader = new SequenceReader<byte>(buffer);
					if (seqReader.TryReadTo(out ReadOnlySequence<byte> lineSeq, (ReadOnlySpan<byte>)newline, advancePastDelimiter: true))
					{
						var lineLen = (int)lineSeq.Length;
						var rented = ArrayPool<byte>.Shared.Rent(lineLen);
						try
						{
							lineSeq.CopyTo(rented);
							var line = encoding.GetString(rented.AsSpan(0, lineLen)).TrimEnd('\r', '\n');

							// Trim Unicode BOM (U+FEFF) if present at start of stream
							if (line.StartsWith("\uFEFF", StringComparison.Ordinal)) line = line.TrimStart('\uFEFF');

							var remaining = buffer.Slice(seqReader.Position);
							bool moreContentAvailable = false;
							if (remaining.Length > 0)
							{
								foreach (var seg in remaining)
								{
									var span = seg.Span;
									for (int i = 0; i < span.Length; i++)
									{
										var bb = span[i];
										if (bb != (byte)'\r' && bb != (byte)'\n')
										{
											moreContentAvailable = true;
											break;
										}
									}

									if (moreContentAvailable) break;
								}
							}
							else
							{
								moreContentAvailable = !result.IsCompleted;
							}

							if (string.IsNullOrEmpty(line))
							{
								if (!moreContentAvailable) return null;
								reader.AdvanceTo(seqReader.Position);
								continue;
							}

							if (!line.StartsWith("--")) throw new MultipartParseException("Unable to determine boundary: content does not start with a valid multipart boundary");
							var detectedBoundary = line.Substring(2);
							if (detectedBoundary.EndsWith("--") && !moreContentAvailable)
							{
								detectedBoundary = detectedBoundary.Substring(0, detectedBoundary.Length - 2);
							}

							reader.AdvanceTo(seqReader.Position);
							return detectedBoundary;
						}
						finally
						{
							ArrayPool<byte>.Shared.Return(rented);
						}
					}
					else
					{
						reader.AdvanceTo(buffer.Start, buffer.End);
						if (result.IsCompleted) return null;
						continue;
					}
				}
			}

			try
			{
				while (true)
				{
					var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
					var buffer = result.Buffer;

					if (buffer.Length == 0)
					{
						if (result.IsCompleted) break;
						reader.AdvanceTo(buffer.Start, buffer.End);
						continue;
					}

					var seqReader = new SequenceReader<byte>(buffer);

					// start parsing from logical buffer start; PipeReader ensures unread data begins at buffer.Start
					var parseCursor = buffer.Start;

					if (!firstBoundaryFound)
					{
						if (seqReader.TryReadTo(out ReadOnlySequence<byte> discard, boundaryBytes, advancePastDelimiter: true))
						{
							firstBoundaryFound = true;
							parseCursor = seqReader.Position;
						}
						else
						{
							reader.AdvanceTo(buffer.End);
							if (result.IsCompleted) break;
							continue;
						}
					}

					while (true)
					{
						var slice = buffer.Slice(parseCursor);
						var headerReader = new SequenceReader<byte>(slice);
						ReadOnlySequence<byte> headerSeq;

						// Try CRLFCRLF first. If not present, try LF LF. Use pre-allocated static patterns to avoid allocating per-parse.
						if (!headerReader.TryReadTo(out headerSeq, headerDelimiter, advancePastDelimiter: true))
						{
							// try LF-LF delimiter
							var lfDelimiter = encoding.GetBytes("\n\n");
							headerReader = new SequenceReader<byte>(slice);
							if (!headerReader.TryReadTo(out headerSeq, lfDelimiter, advancePastDelimiter: true))
							{
								break; // need more data for headers
							}
						}

						int headerLen = (int)headerSeq.Length;
						var rentedHeader = ArrayPool<byte>.Shared.Rent(headerLen);
						try
						{
							headerSeq.CopyTo(rentedHeader);
							var headerString = encoding.GetString(rentedHeader.AsSpan(0, headerLen)).Replace("\r\n", "\n");

							string name = null, filename = null, contentType = null;
							foreach (var line in headerString.Split('\n'))
							{
								var t = line.Trim();
								if (t.StartsWith("Content-Disposition", StringComparison.OrdinalIgnoreCase))
								{
									var parts = t.Split(';');
									foreach (var p in parts)
									{
										var kv = p.Split('=');
										if (kv.Length == 2)
										{
											var k = kv[0].Trim().Trim('"');
											var v = kv[1].Trim().Trim('"');
											if (k.Equals("name", StringComparison.OrdinalIgnoreCase)) name = v;
											if (k.Equals("filename", StringComparison.OrdinalIgnoreCase)) filename = v;
										}
									}
								}
								else if (t.StartsWith("Content-Type", StringComparison.OrdinalIgnoreCase))
								{
									var idx = t.IndexOf(':');
									if (idx >= 0) contentType = t.Substring(idx + 1).Trim();
								}
							}

							var bodyRemaining = slice.Slice(headerReader.Position);
							var bodyReader = new SequenceReader<byte>(bodyRemaining);
							if (!bodyReader.TryReadTo(out ReadOnlySequence<byte> bodySeq, boundaryBytes, advancePastDelimiter: false))
							{
								break; // need more data for body
							}

							// Compute absolute positions: headerReader consumed bytes first, then bodyReader
							var absHeaderEnd = buffer.GetPosition(headerReader.Consumed, parseCursor);
							var absDelimStart = buffer.GetPosition(bodyReader.Consumed, absHeaderEnd);
							var absDelimEnd = buffer.GetPosition(boundaryBytes.Length, absDelimStart);
							var afterSlice = buffer.Slice(absDelimEnd);
							var afterReader = new SequenceReader<byte>(afterSlice);

							if (afterSlice.Length < 2 && !result.IsCompleted)
							{
								break; // need more data to determine final marker
							}

							bool isFinal = false;
							if (afterReader.TryPeek(out byte next) && next == (byte)'-')
							{
								// check for trailing '--'
								if (afterSlice.Length >= 2)
								{
									if (afterSlice.IsSingleSegment)
									{
										var span = afterSlice.First.Span;
										if (span.Length >= 2 && span[0] == (byte)'-' && span[1] == (byte)'-') isFinal = true;
									}
									else
									{
										// build small two-byte buffer (reused)
										afterSlice.Slice(0, 2).CopyTo(twoByteBuffer.AsSpan(0, 2));
										if (twoByteBuffer[0] == (byte)'-' && twoByteBuffer[1] == (byte)'-') isFinal = true;
									}
								}
							}

							// skip optional CRLF after boundary marker
							while (afterReader.TryPeek(out byte b) && (b == (byte)'\r' || b == (byte)'\n')) afterReader.TryRead(out _);

							var newConsumed = buffer.GetPosition(afterReader.Consumed, absDelimEnd);

							int bodyLenTotal = (int)bodySeq.Length;
							int bodyLen = bodyLenTotal;
							if (bodyLenTotal >= 2)
							{
								bodySeq.Slice(bodyLenTotal - 2, 2).CopyTo(lastTwoBuffer.AsSpan(0, 2));
								if (lastTwoBuffer[0] == (byte)'\r' && lastTwoBuffer[1] == (byte)'\n') bodyLen -= 2;
							}

							if (bodyLen == bodyLenTotal && bodyLenTotal >= 1)
							{
								bodySeq.Slice(bodyLenTotal - 1, 1).CopyTo(lastOneBuffer);
								if (lastOneBuffer[0] == (byte)'\n') bodyLen -= 1;
							}

							// If the header contained no well-known parameters at all, treat this as an invalid
							// part and skip it (matches stream-based parser behavior which returns false
							// for IsFilePart/IsParameterPart when parameters.Count == 0).
							if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(filename) && string.IsNullOrEmpty(contentType))
							{
								// advance cursor and continue to next part
								parseCursor = newConsumed;

								if (isFinal)
								{
									reader.AdvanceTo(parseCursor);
									await reader.CompleteAsync().ConfigureAwait(false);
									StreamClosedHandler?.Invoke();
									return;
								}

								// expose consumed and continue
								reader.AdvanceTo(parseCursor, buffer.End);
								continue;
							}

							// Determine if this section should be treated as a file or a parameter.
							// Match the heuristics used by the stream-based parser: it's a file
							// when a filename is present, when the content-type is a known
							// binary mime type, or when the name is missing.
							bool isFile = false;
							if (!string.IsNullOrEmpty(filename)) isFile = true;
							else if (!string.IsNullOrEmpty(contentType) && _options?.BinaryMimeTypes != null && _options.BinaryMimeTypes.Contains(contentType)) isFile = true;
							else if (string.IsNullOrEmpty(name)) isFile = true;

							if (!isFile)
							{
								var paramArray = new byte[bodyLen];
								bodySeq.Slice(0, bodyLen).CopyTo(paramArray);
								var param = new ParameterPartBinary(name ?? string.Empty, new List<byte[]> { paramArray });

								_lastWasFile = false;
								ParameterHandler?.Invoke(param);
							}
							else
							{
								var rented = ArrayPool<byte>.Shared.Rent(bodyLen);
								try
								{
									bodySeq.Slice(0, bodyLen).CopyTo(rented);

									// determine partNumber: if same file as last emission, increment part number, otherwise reset to 0
									var currentKey = (name ?? string.Empty) + "|" + (filename ?? string.Empty) + "|" + (contentType ?? string.Empty);
									int partNumber = 0;
									if (_lastWasFile && _lastFileKey == currentKey)
									{
										partNumber = ++_lastPartNumber;
									}
									else
									{
										_lastFileKey = currentKey;
										_lastPartNumber = 0;
										partNumber = 0;
									}

									_lastWasFile = true;
									FileHandler?.Invoke(name, filename, contentType, "form-data", rented, bodyLen, partNumber, null);
								}
								finally
								{
									ArrayPool<byte>.Shared.Return(rented);
								}
							}

							parseCursor = newConsumed;

							if (isFinal)
							{
								reader.AdvanceTo(parseCursor);
								await reader.CompleteAsync().ConfigureAwait(false);
								StreamClosedHandler?.Invoke();
								return;
							}
						}
						finally
						{
							ArrayPool<byte>.Shared.Return(rentedHeader);
						}
					}

					// advance to parseCursor (what we've consumed) and expose the rest as unread
					reader.AdvanceTo(parseCursor, buffer.End);
					if (result.IsCompleted) break;
				}
			}
			finally
			{
				await reader.CompleteAsync().ConfigureAwait(false);
				StreamClosedHandler?.Invoke();
			}
		}
	}
}
#endif
