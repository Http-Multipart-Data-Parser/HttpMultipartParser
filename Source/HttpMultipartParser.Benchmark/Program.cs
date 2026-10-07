using BenchmarkDotNet.Running;

namespace HttpMultipartParser.Benchmark
{
	class Program
	{
		static void Main(string[] args)
		//=> BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
		=> BenchmarkSwitcher.FromTypes([typeof(MultipartFormDataParserBenchmark)]).Run(args);
	}
}
