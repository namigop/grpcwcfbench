public enum WcfTransport { BasicHttp, WsHttp, NetTcp }


public record BenchResult(
    double[] Latencies,
    int Errors,
    TimeSpan Elapsed,
    long AllocatedBytes = 0,
    int Gen0Collections = 0,
    int Gen1Collections = 0,
    int Gen2Collections = 0,
    long PeakWorkingSetBytes = 0,
    int OrdersPerCall = 0) {
    public int Successes => Latencies.Length;
    public double OrdersPerSecond
        => OrdersPerCall <= 0 ? 0 : Successes * (double)OrdersPerCall / Elapsed.TotalSeconds;
}
