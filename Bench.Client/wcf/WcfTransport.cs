public enum WcfTransport { BasicHttp, WsHttp, NetTcp }
public record BenchResult(
    double[] Latencies, int Errors,  TimeSpan Elapsed)
{
    public int Successes => Latencies.Length;
}
