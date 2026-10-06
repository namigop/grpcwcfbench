using System.Diagnostics;
using System.Threading;
using Bench.Client;

namespace Bench;

public static class Harness {
    static double Percentile(double[] sorted, double p) {
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    public static string CreateReport(BenchResult r) {
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

    static void EnsureThreadPoolCapacity(int concurrency) {
        ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionThreads);
        int wanted = concurrency + Environment.ProcessorCount;
        if (minWorkerThreads < wanted)
            ThreadPool.SetMinThreads(wanted, minCompletionThreads);
    }

    public static async Task<BenchResult> Run(Args args, Func<int, CancellationToken, Task> call, bool record) {
        EnsureThreadPoolCapacity(args.Concurrency);

        var latencies = new List<double>[args.Concurrency]; // per-worker => no lock contention
        var errors = new int[args.Concurrency];

        var sw = Stopwatch.StartNew();
        var end = args.Duration;
        
        var workers = Enumerable.Range(0, args.Concurrency)
            //Select(id => Task.Factory.StartNew(async () => await Loop(id), TaskCreationOptions.LongRunning))
            .Select(id => Task.Run(async () => await Loop(id)))
            .ToArray();

        await Task.WhenAll(workers);
        sw.Stop();

        var all = latencies.SelectMany(l => l).ToArray();
        Array.Sort(all);
        return new BenchResult(
            all,
            errors.Sum(),
            sw.Elapsed);

        async Task Loop(int id) {
            {
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
            }
        }
    }
}
