using System.Diagnostics;
using System.Threading;
using Bench.Client;

namespace Bench;

public static class Harness {
    static double Percentile(double[] sorted, double p) {
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    static string Bytes(long n) => n switch {
        >= 1L << 30 => $"{n / (double)(1L << 30):0.00} GB",
        >= 1L << 20 => $"{n / (double)(1L << 20):0.00} MB",
        >= 1L << 10 => $"{n / (double)(1L << 10):0.0} KB",
        _ => $"{n:N0} B"
    };

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
                      Memory / GC
                         Allocated  : {Bytes(r.AllocatedBytes)}
                         GC 0/1/2   : {r.Gen0Collections:N0} / {r.Gen1Collections:N0} / {r.Gen2Collections:N0}
                         Peak WS    : {Bytes(r.PeakWorkingSetBytes)}
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

        // Snapshot before the workers start so the warmup pass (same harness, discarded result)
        // does not leak into the measured figures.
        long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);

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
            sw.Elapsed,
            GC.GetTotalAllocatedBytes(precise: true) - allocBefore,
            gen0Before == GC.CollectionCount(0) ? 0 : GC.CollectionCount(0) - gen0Before,
            gen1Before == GC.CollectionCount(1) ? 0 : GC.CollectionCount(1) - gen1Before,
            gen2Before == GC.CollectionCount(2) ? 0 : GC.CollectionCount(2) - gen2Before,
            Bench.Client.PeakWorkingSet.Bytes);
        
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
