using BenchmarkDotNet.Attributes;

namespace HttpMultipartParser.Benchmark
{
	[MemoryDiagnoser]
	[HtmlExporter]
	[JsonExporter]
	[MarkdownExporter]
	public class MultipartFormDataParserBenchmark
	{
		private readonly Stream small;
		private readonly Stream medium;
		private readonly Stream large;

		private readonly Stream small_pipelines;
		private readonly Stream medium_pipelines;
		private readonly Stream large_pipelines;

		public MultipartFormDataParserBenchmark()
		{
			small = new BenchmarkData(5, 10, 1, 125000).ToStream();
			medium = new BenchmarkData(25, 50, 5, 250000).ToStream();
			large = new BenchmarkData(100, 500, 50, 500000).ToStream();

			small_pipelines = new BenchmarkData(5, 10, 1, 125000).ToStream();
			medium_pipelines = new BenchmarkData(25, 50, 5, 250000).ToStream();
			large_pipelines = new BenchmarkData(100, 500, 50, 500000).ToStream();
		}

		[Benchmark]
		public async Task<MultipartFormDataParser> Small()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary"
			};

			small.Position = 0;
			return await MultipartFormDataParser.ParseAsync(small, options, CancellationToken.None).ConfigureAwait(false);
		}

		[Benchmark]
		public async Task<MultipartFormDataParserPipelines> Small_Pipelines()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary"
			};

			small_pipelines.Position = 0;
			return await MultipartFormDataParserPipelines.ParseAsync(small_pipelines, options, CancellationToken.None).ConfigureAwait(false);
		}

		[Benchmark]
		public async Task<MultipartFormDataParser> Medium()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary"
			};

			medium.Position = 0;
			return await MultipartFormDataParser.ParseAsync(medium, options, CancellationToken.None).ConfigureAwait(false);
		}

		[Benchmark]
		public async Task<MultipartFormDataParserPipelines> Medium_Pipelines()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary"
			};

			medium_pipelines.Position = 0;
			return await MultipartFormDataParserPipelines.ParseAsync(medium_pipelines, options, CancellationToken.None).ConfigureAwait(false);
		}

		[Benchmark]
		public async Task<MultipartFormDataParser> Large()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary"
			};

			large.Position = 0;
			return await MultipartFormDataParser.ParseAsync(large, options, CancellationToken.None).ConfigureAwait(false);
		}

		[Benchmark]
		public async Task<MultipartFormDataParserPipelines> Large_Pipelines()
		{
			var options = new ParserOptions
			{
				Boundary = "boundary"
			};

			large_pipelines.Position = 0;
			return await MultipartFormDataParserPipelines.ParseAsync(large_pipelines, options, CancellationToken.None).ConfigureAwait(false);
		}
	}
}
