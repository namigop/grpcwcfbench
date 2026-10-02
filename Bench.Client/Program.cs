// Bench.Client — WCF and gRPC benchmark client.
//
// Usage:
//   Bench.Client -target wcf  -arg basichttp   WCF over BasicHttpBinding (http://localhost:5000/NorthwindWcfService/basic)
//   Bench.Client -target wcf  -arg wshttp      WCF over WSHttpBinding   (https://localhost:5001/NorthwindWcfService/ws)
//   Bench.Client -target wcf  -arg nettcp      WCF over NetTcpBinding   (net.tcp://localhost:5002/NorthwindWcfService/nettcp)
//   Bench.Client -target grpc                  gRPC over HTTP/2         (http://localhost:5003)
//
// Benchmark options (omit all three for a single-call smoke test that echoes the response JSON):
//   -w <seconds>   warmup duration before measurement starts (default 0)
//   -d <seconds>   measurement duration                        (default 0 = single call)
//   -c <count>     concurrent workers                          (default 1)
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
    // No timing flags at all means "just prove the endpoint works": one call, echoed as JSON.
    bool smokeTest = cli.Warmup is null && cli.Duration is null;

    if (smokeTest) {
        object? response = cli.UseGrpc
            ? await new GrpcPerf(cli.Address).Call(0)
            : await new Perf(cli.Transport, new Args(cli.Address, 1, TimeSpan.Zero, TimeSpan.Zero)).Call(0);

        Console.WriteLine(JsonSerializer.Serialize(response));
        return 0;
    }

    var benchArgs = new Args(
        Address: cli.Address,
        Concurrency: cli.Concurrency,
        Duration: TimeSpan.FromSeconds(cli.Duration ?? 0),
        Warmup: TimeSpan.FromSeconds(cli.Warmup ?? 0));

    BenchResult result = cli.UseGrpc
        ? await RunGrpcAsync(benchArgs)
        : await RunWcfAsync(cli.Transport, benchArgs);

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

static async Task<BenchResult> RunWcfAsync(WcfTransport transport, Args args) {
    // One client per worker: a WCF ClientBase drives a single channel, so sharing one across
    // workers would serialise the calls and make -c meaningless.
    var clients = new Perf[args.Concurrency];
    try {
        for (int i = 0; i < clients.Length; i++) clients[i] = new Perf(transport, args);

        // Warmup reuses the same clients so connection setup and JIT land outside the measurement.
        if (args.Warmup > TimeSpan.Zero) {
            await Harness.Run(args with { Duration = args.Warmup }, (id, ct) => clients[id].Call(id, ct), record: false);
        }

        return await Harness.Run(args, (id, ct) => clients[id].Call(id, ct), record: true);
    } finally {
        foreach (Perf client in clients) client.Dispose();
    }
}

static async Task<BenchResult> RunGrpcAsync(Args args) {
    // One channel shared by every worker: HTTP/2 multiplexes concurrent calls over one connection.
    using var perf = new GrpcPerf(args.Address);

    if (args.Warmup > TimeSpan.Zero) {
        await Harness.Run(args with { Duration = args.Warmup }, (id, ct) => perf.Call(id, ct), record: false);
    }

    return await Harness.Run(args, (id, ct) => perf.Call(id, ct), record: true);
}