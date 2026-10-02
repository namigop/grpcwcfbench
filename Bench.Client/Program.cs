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
    // No timing flags. just to check if the service is up
    bool smokeTest = cli.Warmup is null && cli.Duration is null;
    if (smokeTest) {
        var response = cli.UseGrpc
            ? await new GrpcPerf(cli.Address).Call(0)
            : await new WcfPerf(cli.Transport, new Args(cli.Address, 1, TimeSpan.Zero, TimeSpan.Zero)).Call(0);

        Console.WriteLine(JsonSerializer.Serialize(response));
        return 0;
    }

    var benchArgs = new Args(
        Address: cli.Address,
        Concurrency: cli.Concurrency,
        Duration: TimeSpan.FromSeconds(cli.Duration ?? 0),
        Warmup: TimeSpan.FromSeconds(cli.Warmup ?? 0));

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
    var clients = Enumerable.Range(0, args.Concurrency).Select(_ => new GrpcPerf(args.Address)).ToArray();    
    try {
        var callers = clients.Select<GrpcPerf, Func<int, CancellationToken, Task<bool>>>(c => c.Call).ToArray();
        return await Run(callers, args);
    } finally {
        foreach (var client in clients)
            client.Dispose();
    }
}