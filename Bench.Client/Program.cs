// Bench.Client — WCF and gRPC benchmark dispatcher.
//
// Usage:
//   Bench.Client -target wcf  -arg basichttp   WCF over BasicHttpBinding (http://localhost:5000/NorthwindWcfService/basic)
//   Bench.Client -target wcf  -arg wshttp      WCF over WSHttpBinding   (https://localhost:5001/NorthwindWcfService/ws)
//   Bench.Client -target wcf  -arg nettcp      WCF over NetTcpBinding   (net.tcp://localhost:5002/NorthwindWcfService/nettcp)
//   Bench.Client -target grpc                  gRPC over HTTP/2         (http://localhost:5003)
//
// Environment overrides (all optional):
//   BENCH_WCF_TCP_URL   Override the NetTcp endpoint address (default: net.tcp://localhost:5002/NorthwindWcfService/nettcp)
//   BENCH_GRPC_URL      Override the gRPC channel address        (default: http://localhost:5003)

using System.ServiceModel;
using System.Text.Json;
using System.Xml;
using Bench;
using Bench.Client;
using Bench.Client.wcf;
using Bench.Client.Wcf;
using Bench.wcf.DataContracts;
using Grpc.Net.Client;
// Avoid `using Northwind;` to keep `OrderRequest` unambiguous with the WCF data contract of the same name.
// gRPC types from the Northwind proto are referenced fully-qualified below.

const string DefaultNetTcpUrl = "net.tcp://localhost:5002/NorthwindWcfService/nettcp";
const string DefaultGrpcUrl  = "http://localhost:5003";

if (!TryParseArgs(args, out var target, out var arg, out var parseError)) {
    Console.Error.WriteLine(parseError);
    PrintUsage(Console.Error);
    return 2;
}

try {
    return (target, arg) switch {
        ("wcf", "basic") => await RunWcfAsync(WcfTransport.BasicHttp),
        ("wcf", "ws")    => await RunWcfAsync(WcfTransport.WsHttp),
        ("wcf", "net")    => await RunWcfAsync(WcfTransport.NetTcp),
        ("grpc", null)       => await RunGrpcAsync(),
        ("grpc", _)          => Fail($"-arg '{arg}' is not valid with -target grpc"),
        _                    => Fail($"Unknown combination: -target {target} -arg {arg}")
    };

} catch (Exception ex) {
    Console.Error.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

static bool TryParseArgs(string[] argv, out string? target, out string? arg, out string? error) {
    target = null;
    arg = null;
    error = null;
    for (var i = 0; i < argv.Length; i++) {
        switch (argv[i]) {
            case "-target":
                if (i + 1 >= argv.Length) { error = "-target requires a value"; return false; }
                target = argv[++i];
                break;
            case "-arg":
                if (i + 1 >= argv.Length) { error = "-arg requires a value"; return false; }
                arg = argv[++i];
                break;
            default:
                error = $"Unknown argument: {argv[i]}";
                return false;
        }
    }
    return true;
}

static void PrintUsage(TextWriter w) {
    w.WriteLine("Usage:");
    w.WriteLine("  Bench.Client -target wcf  -arg basic");
    w.WriteLine("  Bench.Client -target wcf  -arg ws");
    w.WriteLine("  Bench.Client -target wcf  -arg net");
    w.WriteLine("  Bench.Client -target grpc");
    w.WriteLine();
    // w.WriteLine("Environment overrides:");
    // w.WriteLine("  BENCH_WCF_TCP_URL   NetTcp endpoint (default: net.tcp://localhost:5002/NorthwindWcfService/nettcp)");
    // w.WriteLine("  BENCH_GRPC_URL      gRPC channel    (default: http://localhost:5003)");
}

static int Fail(string message) {
    Console.Error.WriteLine(message);
    PrintUsage(Console.Error);
    return 2;
}

async Task<int> RunWcfAsync(WcfTransport transport) {
    Args CreateArgs() {
        var address = transport switch {
            WcfTransport.BasicHttp => "http://localhost:5000/NorthwindWcfService/basic",
            WcfTransport.WsHttp => "https://localhost:5001/NorthwindWcfService/ws",
            WcfTransport.NetTcp => "net.tcp://localhost:5002/NorthwindWcfService/nettcp",
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        };
        return new Args(address, 1, TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(30));
    }

    var args = CreateArgs();
    var wcf = new Perf(transport, args);
    var result = await Harness.Run(args, wcf.Call, true);
    Console.WriteLine(Harness.CreateReport(result));
    return 0;
}

async Task<int> RunGrpcAsync() {
    using var channel = GrpcChannel.ForAddress("TODO");
    var client = new Northwind.NorthwindService.NorthwindServiceClient(channel);
    var reply = await client.GetOrderByIdAsync(new Northwind.OrderRequest { OrderId = 10248 });
    Console.WriteLine(JsonSerializer.Serialize(reply));
    return 0;
}
  