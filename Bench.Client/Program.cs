// Bench.Client — WCF and gRPC benchmark dispatcher.
//
// Usage:
//   Bench.Client -target wcf  -arg basichttp   WCF over BasicHttpBinding (http://localhost:5000/NorthwindWcfService/basic)
//   Bench.Client -target wcf  -arg wshttp      WCF over WSHttpBinding   (https://localhost:5001/NorthwindWcfService/ws)
//   Bench.Client -target wcf  -arg nettcp      WCF over NetTcpBinding   (net.tcp://localhost:5002/NorthwindWcfService/nettcp)
//   Bench.Client -target grpc                  gRPC over HTTP/2         (http://localhost:5003)

using System.ServiceModel;
using System.Text.Json;
using Bench.Client.Wcf;
using Bench.wcf.DataContracts;
using Grpc.Net.Client;
// Avoid `using Northwind;` to keep `OrderRequest` unambiguous with the WCF data contract of the same name.
// gRPC types from the Northwind proto are referenced fully-qualified below.

if (!TryParseArgs(args, out var target, out var arg, out var parseError)) {
    Console.Error.WriteLine(parseError);
    PrintUsage(Console.Error);
    return 2;
}

try {
    return (target, arg) switch {
        ("wcf", "basichttp") => await RunWcfAsync(WcfTransport.BasicHttp),
        ("wcf", "wshttp")    => await RunWcfAsync(WcfTransport.WsHttp),
        ("wcf", "nettcp")    => await RunWcfAsync(WcfTransport.NetTcp),
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
    w.WriteLine("  Bench.Client -target wcf  -arg basichttp");
    w.WriteLine("  Bench.Client -target wcf  -arg wshttp");
    w.WriteLine("  Bench.Client -target wcf  -arg nettcp");
    w.WriteLine("  Bench.Client -target grpc");
}

static int Fail(string message) {
    Console.Error.WriteLine(message);
    PrintUsage(Console.Error);
    return 2;
}

async Task<int> RunWcfAsync(WcfTransport transport) {
    NorthwindServiceClient client = transport switch {
        WcfTransport.BasicHttp => new NorthwindServiceClient(NorthwindServiceClient.EndpointConfiguration.BasicHttpBinding_INorthwindService),
        WcfTransport.WsHttp    => new NorthwindServiceClient(NorthwindServiceClient.EndpointConfiguration.WSHttpBinding_INorthwindService),
        WcfTransport.NetTcp    => new NorthwindServiceClient(
                                       new NetTcpBinding(SecurityMode.None),
                                       new EndpointAddress("net.tcp://localhost:5002/NorthwindWcfService/nettcp")),
        _                      => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    try {
        var order = await client.GetOrderByIdAsync(new OrderRequest { OrderId = 10248 });
        Console.WriteLine(JsonSerializer.Serialize(order));
        return 0;
    } finally {
        await client.CloseAsync();
    }
}

async Task<int> RunGrpcAsync() {
    using var channel = GrpcChannel.ForAddress("http://localhost:5003");
    var client = new Northwind.NorthwindService.NorthwindServiceClient(channel);
    var reply = await client.GetOrderByIdAsync(new Northwind.OrderRequest { OrderId = 10248 });
    Console.WriteLine(JsonSerializer.Serialize(reply));
    return 0;
}

internal enum WcfTransport { BasicHttp, WsHttp, NetTcp }