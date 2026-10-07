using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class TinyData
	{
		private readonly ITestOutputHelper _outputHelper;
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary
            Content-Disposition: form-data; name=""text""

            textdata
            --boundary
            Content-Disposition: form-data; name=""file""; filename=""data.txt""
            Content-Type: text/plain

            tiny
            --boundary
            Content-Disposition: form-data; name=""after""

            afterdata
            --boundary--"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			new List<ParameterPart> {
				new ParameterPart("text", "textdata"),
				new ParameterPart("after", "afterdata")
			},
			new List<FilePart> {
				new FilePart( "file", "data.txt", TestUtil.StringToStreamNoBom("tiny"))
			}
		);

		public TinyData(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
			foreach (var filePart in _testCase.ExpectedFileData)
			{
				filePart.Data.Position = 0;
			}
		}

		/// <summary>
		///     Tests for correct detection of the boundary in the input stream.
		/// </summary>
		[Fact]
		public void CanAutoDetectBoundary()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8,
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Tests for correct detection of the boundary in the input stream.
		/// </summary>
		[Fact]
		public async Task CanAutoDetectBoundaryAsync()
		{
			var options = new ParserOptions
			{
				Encoding = Encoding.UTF8,
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Ensures that boundary detection works even when the boundary spans
		///     two different buffers.
		/// </summary>
		[Fact]
		public void CanDetectBoundariesCrossBuffer()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = 16,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Ensures that boundary detection works even when the boundary spans
		///     two different buffers.
		/// </summary>
		[Fact]
		public async Task CanDetectBoundariesCrossBufferAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				BinaryBufferSize = 16,
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Ensure that mixed newline formats are correctly handled.
		/// </summary>
		[Fact]
		public void CorrectlyHandleMixedNewlineFormats()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			// Replace the first '\n' with '\r\n'
			var regex = new Regex(Regex.Escape("\n"));
			string request = regex.Replace(_testCase.Request, "\r\n", 1);
			using (Stream stream = TestUtil.StringToStream(request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Ensure that mixed newline formats are correctly handled.
		/// </summary>
		[Fact]
		public async Task CorrectlyHandleMixedNewlineFormatsAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			// Replace the first '\n' with '\r\n'
			var regex = new Regex(Regex.Escape("\n"));
			string request = regex.Replace(_testCase.Request, "\r\n", 1);
			using (Stream stream = TestUtil.StringToStream(request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Tests for correct handling of <c>crlf (\r\n)</c> in the input stream.
		/// </summary>
		[Fact]
		public void CorrectlyHandlesCRLF()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			string request = _testCase.Request.Replace("\n", "\r\n");
			using (Stream stream = TestUtil.StringToStream(request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     Tests for correct handling of <c>crlf (\r\n)</c> in the input stream.
		/// </summary>
		[Fact]
		public async Task CorrectlyHandlesCRLFAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			string request = _testCase.Request.Replace("\n", "\r\n");
			using (Stream stream = TestUtil.StringToStream(request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     The tiny data test.
		/// </summary>
		[Fact]
		public void TinyDataTest()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		/// <summary>
		///     The tiny data test.
		/// </summary>
		[Fact]
		public async Task TinyDataTestAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));
			}
		}

		[Fact]
		public void DoesNotCloseTheStream()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.True(_testCase.Validate(parser, _outputHelper));

				stream.Position = 0;
				Assert.True(true, "A closed stream would throw ObjectDisposedException");
			}
		}

		[Fact]
		public async Task DoesNotCloseTheStreamAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.True(_testCase.Validate(parser, _outputHelper));

				stream.Position = 0;
				Assert.True(true, "A closed stream would throw ObjectDisposedException");
			}
		}

		[Fact]
		public void GetParameterValueReturnsNullIfNoParameterFound()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.Null(parser.GetParameterValue("does not exist"));
			}
		}

		[Fact]
		public async Task GetParameterValueReturnsNullIfNoParameterFoundAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.Null(parser.GetParameterValue("does not exist"));
			}
		}

		[Fact]
		public void GetParameterValueReturnsCorrectlyWithoutComparisonType()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.NotNull(parser.GetParameterValue("text"));
				Assert.Null(parser.GetParameterValue("Text"));
			}
		}

		[Fact]
		public async Task GetParameterValueReturnsCorrectlyWithoutComparisonTypeAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.NotNull(parser.GetParameterValue("text"));
				Assert.Null(parser.GetParameterValue("Text"));
			}
		}

		[Fact]
		public void GetParameterValueReturnsCorrectlyWithComparisonType()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = MultipartFormDataParser.Parse(stream, options);
				Assert.NotNull(parser.GetParameterValue("text", StringComparison.OrdinalIgnoreCase));
				Assert.NotNull(parser.GetParameterValue("Text", StringComparison.OrdinalIgnoreCase));
			}
		}

		[Fact]
		public async Task GetParameterValueReturnsCorrectlyWithComparisonTypeAsync()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary",
				Encoding = Encoding.UTF8
			};

			using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
			{
				var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
				Assert.NotNull(parser.GetParameterValue("text", StringComparison.OrdinalIgnoreCase));
				Assert.NotNull(parser.GetParameterValue("Text", StringComparison.OrdinalIgnoreCase));
			}
		}

		[Fact]
		public void CanDetectBoundriesWithNewLineInNextBuffer()
		{
			var startIndex = 16;
			for (int i = startIndex; i < _testCase.Request.Length; i++)
			{
				_outputHelper.WriteLine($"Test {i - startIndex + 1}/{_testCase.Request.Length - startIndex}; buffer length: {i}");

				var options = new ParserOptions
				{
					Boundary = "boundary",
					BinaryBufferSize = i,
					Encoding = Encoding.UTF8
				};

				using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
				{
					var parser = MultipartFormDataParser.Parse(stream, options);
					Assert.True(_testCase.Validate(parser, _outputHelper), $"Failure in buffer length {i}");
				}

				_outputHelper.WriteLine(""); // Blank line for better readability in the output
			}
		}

		[Fact]
		public async Task CanDetectBoundriesWithNewLineInNextBufferAsync()
		{
			var startIndex = 16;
			for (int i = startIndex; i < _testCase.Request.Length; i++)
			{
				_outputHelper.WriteLine($"Test {i - startIndex + 1}/{_testCase.Request.Length - startIndex}; buffer length: {i}");

				var options = new ParserOptions
				{
					Boundary = "boundary",
					BinaryBufferSize = i,
					Encoding = Encoding.UTF8
				};

				using (Stream stream = TestUtil.StringToStream(_testCase.Request, options.Encoding))
				{
					var parser = await MultipartFormDataParser.ParseAsync(stream, options, TestContext.Current.CancellationToken);
					Assert.True(_testCase.Validate(parser, _outputHelper), $"Failure in buffer length {i}");
				}

				_outputHelper.WriteLine(""); // Blank line for better readability in the output
			}
		}
	}
}
