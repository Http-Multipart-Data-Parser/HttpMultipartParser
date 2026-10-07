using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HttpMultipartParser.UnitTests.ParserScenarios
{
	public class BoundaryEndsWithDoubleDash
	{
		private readonly ITestOutputHelper _outputHelper;
		// The boundary in this scenario ends with '--'. This is an unusual scenario but perfectly legitimate.
		// For details, see: https://github.com/Http-Multipart-Data-Parser/Http-Multipart-Data-Parser/issues/123
		private static readonly string _testData = TestUtil.TrimAllLines(
			@"--boundary_text--
			Content-type: text/plain; charset=UTF-8
			Content-Disposition: form-data; name=""file1""; filename=""file1.txt""

			file content here
			--boundary_text--
			Content-type: text/plain; charset=UTF-8
			Content-Disposition: form-data; name=""file2""; filename=""file2.txt""

			file content here 2
			--boundary_text----"
		);

		private static readonly TestData _testCase = new TestData(
			_testData,
			Enumerable.Empty<ParameterPart>().ToList(),
			new List<FilePart>() {
				new FilePart("file1", "file1.txt", TestUtil.StringToStreamNoBom("file content here"), (new[] { new KeyValuePair<string, string>("charset", "UTF-8") }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)),
				new FilePart("file2", "file2.txt", TestUtil.StringToStreamNoBom("file content here 2"), (new[] { new KeyValuePair<string, string>("charset", "UTF-8") }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)),
			}
		);

		public BoundaryEndsWithDoubleDash(ITestOutputHelper outputHelper)
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
	}
}
