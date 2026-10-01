using System.Diagnostics;
using System.ServiceModel;
using System.Text.Json;
using System.Xml;
using Bench.Client;
using Bench.Client.Wcf;
using Bench.wcf.DataContracts;

namespace Bench;

public static class Harness {
    static double Percentile(double[] sorted, double p) {
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    public static string CreateReport(BenchResult r) {
        // Every call can fail (e.g. the server is not running), leaving no samples to report.
        // Say so plainly instead of throwing IndexOutOfRange and hiding the real cause.
        if (r.Latencies.Length == 0) {
            return $"""
                    ================ Results ================
                    Elapsed      : {r.Elapsed.TotalSeconds:F2}s
                    Successful   : 0
                    Failed       : {r.Errors:N0}
                    No successful calls, so there is no latency to report.
                    """;
        }

        var report = $"""
                      ================ Results ================
                      Elapsed      : {r.Elapsed.TotalSeconds:F2}s
                      Successful   : {r.Successes:N0}
                      Failed       : {r.Errors:N0}
                      Throughput   : {r.Successes / r.Elapsed.TotalSeconds:N0} req/s
                      Latency (ms)
                         min   : {r.Latencies[0]:F3}
                         mean  : {r.Latencies.Average():F3}
                         p50   : {Percentile(r.Latencies, 50):F3}
                         p90   : {Percentile(r.Latencies, 90):F3}
                         p95   : {Percentile(r.Latencies, 95):F3}
                         p99   : {Percentile(r.Latencies, 99):F3}
                         max   : {r.Latencies[^1]:F3}
                      """;
        return report;
    }

    public static async Task<BenchResult> Run(Args args, Func<int, CancellationToken, Task> call, bool record) {
        using var cts = new CancellationTokenSource();
        var latencies = new List<double>[args.Concurrency]; // per-worker => no lock contention
        var errors = new int[args.Concurrency];

        var sw = Stopwatch.StartNew();
        var end = args.Duration;
        
        var workers = Enumerable.Range(0, args.Concurrency).Select(id => Task.Run(async () => {
            var list = new List<double>();
            latencies[id] = list;

            //make the call as soon as the previous one completes.
            while (sw.Elapsed < end) {
                long start = Stopwatch.GetTimestamp();
                try {
                    await call(id, CancellationToken.None);
                    if (record)
                        list.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
                catch (Exception exc) {
                    errors[id]++;
                }
            }
        })).ToArray();

        await Task.WhenAll(workers);
        sw.Stop();

        var all = latencies.SelectMany(l => l).ToArray();
        Array.Sort(all);
        return new BenchResult(all, errors.Sum(), sw.Elapsed);
    }
}