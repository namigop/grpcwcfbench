namespace Bench.Client;

public record Args(string Address, int Concurrency, TimeSpan Duration, TimeSpan Warmup);