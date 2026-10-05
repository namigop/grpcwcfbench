// Bench.Client — WCF and gRPC benchmark client.
//
// Usage:
//   Bench.Client -target wcf  -arg basichttp   WCF over BasicHttpBinding (http://localhost:5000/NorthwindWcfService/basic)
//   Bench.Client -target wcf  -arg wshttp      WCF over WSHttpBinding   (https://localhost:5001/NorthwindWcfService/ws)
//   Bench.Client -target wcf  -arg nettcp      WCF over NetTcpBinding   (net.tcp://localhost:5002/NorthwindWcfService/nettcp)
//   Bench.Client -target grpc                  gRPC over HTTP/2         (http://localhost:5003)
//
// Operations:
//   -op getorderbyid  single order (default)
//   -op getorders     batch of orders, 10000 by default
//
// Benchmark options (omit -w/-d/-c for a single-call smoke test that echoes the response):
//   -w <seconds>   warmup duration before measurement starts (default 0)
//   -d <seconds>   measurement duration                        (default 0 = single call)
//   -c <count>     concurrent workers                          (default 1)
//
// Batch options (-op getorders only):
//   -n <count>     orders per call; 0 or omitted means the server default (10000)
//
// Overrides:
//   -address <uri>  override the endpoint address for the selected target
//   BENCH_GRPC_URL  override the gRPC address (same effect as -address for -target grpc)

using System.ServiceModel;
using System.Text.Json;
using Bench;
using Bench.Client;
using Bench.Client.grpc;
using Bench.Client.wcf;

if (!Cli.TryParse(args, out Cli cli, out string? parseError)) {
    Console.Error.WriteLine(parseError);
    Cli.PrintUsage(Console.Error);
    return 2;
}

try {
    var benchArgs = new Args(
        Address: cli.Address,
        Concurrency: cli.Concurrency,
        Duration: TimeSpan.FromSeconds(cli.Duration ?? 0),
        Warmup: TimeSpan.FromSeconds(cli.Warmup ?? 0),
        Operation: cli.Operation,
        Count: cli.Count);

    // No timing flags. just to check if the service is up
    bool smokeTest = cli.Warmup is null && cli.Duration is null;
    if (smokeTest) {
        bool ok;
        int? orders;
        if (cli.UseGrpc) {
            using var client = new GrpcPerf(benchArgs.Address, benchArgs.Operation, benchArgs.Count);
            ok = await client.Call(0);
            orders = client.LastOrders;
        } else {
            using var client = new WcfPerf(cli.Transport, benchArgs);
            ok = await client.Call(0);
            orders = client.LastOrders;
        }

        // Keep the historical bare-bool output for the single order smoke test, and show the
        // batch size for getorders, where "true" on its own says almost nothing.
        Console.WriteLine(benchArgs.Operation == BenchOperation.GetOrders
            ? JsonSerializer.Serialize(new { ok, orders = orders ?? 0 })
            : JsonSerializer.Serialize(ok));
        return ok ? 0 : 1;
    }

    WarnIfLargeBatch(benchArgs);

    BenchResult result = cli.UseGrpc
        ? await RunGrpc(benchArgs)
        : await RunWcf(cli.Transport, benchArgs);

    Console.WriteLine($"{cli.Describe()}  (warmup {benchArgs.Warmup.TotalSeconds:0.###}s, duration {benchArgs.Duration.TotalSeconds:0.###}s, concurrency {benchArgs.Concurrency})");
    Console.WriteLine(Harness.CreateReport(result));
    return result.Errors == 0 ? 0 : 1;
} catch (CommunicationException ex) {
    Console.Error.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine("Hint: is the server running? Start it with `Bench.Server -target wcf` or `Bench.Server -target grpc`.");
    return 1;
} catch (Exception ex) {
    Console.Error.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

static void WarnIfLargeBatch(Args args) {
    if (args.Operation != BenchOperation.GetOrders) return;

    // Order-of-magnitude only: roughly 1.5 KB of managed heap per order once the response object
    // graph, the strings and the XML reader buffers are counted. Advisory, never a limit, so a
    // deliberate stress run is still possible. The report's Peak WS is the authoritative figure.
    const long ManagedBytesPerOrderEstimate = 1536;
    long estimate = (long)args.Concurrency * args.EffectiveOrderCount * ManagedBytesPerOrderEstimate;
    if (estimate < 1L << 30) return;

    Console.WriteLine(
        $"WARNING: rough client-side estimate {estimate / (double)(1L << 30):0.0} GB " +
        $"({args.Concurrency} workers x {args.EffectiveOrderCount:N0} orders x ~1.5 KB). " +
        "This is a guess, not a limit. Lower -c or -n if the run thrashes or dies, and compare " +
        "against the Peak WS in the report.");
}

static async Task<BenchResult> Run(Func<int, CancellationToken, Task<bool>>[] callers, Args args) {
   //Warmup
    if (args.Warmup > TimeSpan.Zero) {
        await Harness.Run(args with { Duration = args.Warmup }, (id, ct) => callers[id](id, ct), record: false);
    }

    // Actual run
    return await Harness.Run(args, (id, ct) => callers[id](id, ct), record: true);
}

static async Task<BenchResult> RunWcf(WcfTransport transport, Args args) {
    var clients = Enumerable.Range(0, args.Concurrency).Select(_ => new WcfPerf(transport, args)).ToArray();     
    try {
        var callers = clients.Select<WcfPerf, Func<int, CancellationToken, Task<bool>>>(c => c.Call).ToArray();
        return await Run(callers, args);
    } finally {
        foreach (var client in clients)
            client.Dispose();
    }
}

static async Task<BenchResult> RunGrpc(Args args) {
    var clients = Enumerable.Range(0, args.Concurrency).Select(_ => new GrpcPerf(args.Address, args.Operation, args.Count)).ToArray();    
    try {
        var callers = clients.Select<GrpcPerf, Func<int, CancellationToken, Task<bool>>>(c => c.Call).ToArray();
        return await Run(callers, args);
    } finally {
        foreach (var client in clients)
            client.Dispose();
    }
}
