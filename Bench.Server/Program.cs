// Bench.Server — WCF and gRPC service host.
//
// Usage:
//   Bench.Server -target wcf    Starts the WCF service on BasicHttp (:5000), WSHttp/HTTPS (:5001), and NetTcp (:5002)
//   Bench.Server -target grpc   Starts the gRPC service on HTTP/2 cleartext (:5003)

using Bench.grpc;
using Bench.wcf;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Grpc.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

string? target = null;
for (var i = 0; i < args.Length; i++) {
    if (args[i] == "-target") {
        if (i + 1 >= args.Length) {
            Console.Error.WriteLine("-target requires a value");
            PrintUsage(Console.Error);
            return 2;
        }
        target = args[++i];
    } else {
        Console.Error.WriteLine($"Unknown argument: {args[i]}");
        PrintUsage(Console.Error);
        return 2;
    }
}

if (target is null) {
    PrintUsage(Console.Error);
    return 2;
}

try {
    return target switch {
        "wcf"  => await RunWcfAsync(),
        "grpc" => await RunGrpcAsync(),
        _      => Fail($"Unknown -target '{target}'")
    };
} catch (Exception ex) {
    Console.Error.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

static void PrintUsage(TextWriter w) {
    w.WriteLine("Usage:");
    w.WriteLine("  Bench.Server -target wcf    Start the WCF service");
    w.WriteLine("  Bench.Server -target grpc   Start the gRPC service");
}

static int Fail(string message) {
    Console.Error.WriteLine(message);
    PrintUsage(Console.Error);
    return 2;
}

async Task<int> RunWcfAsync() {
    Console.WriteLine("Starting WCF service...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddServiceModelServices().AddServiceModelMetadata();
    builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

    // HTTP endpoints are configured via ConfigureKestrel (avoids the UseNetTcp-vs-UseUrls collision
    // where UseNetTcp would override URL-based config and break CoreWCF base-address resolution).
    builder.WebHost.ConfigureKestrel(options => {
        options.ListenLocalhost(5000);             // HTTP/1.1 for WCF BasicHttp
        options.ListenLocalhost(5001, o => o.UseHttps()); // HTTPS for WCF WSHttp
    });
    // UseNetTcp with the explicit options form registers a net.tcp base address and opens the TCP listener,
    // without overriding the Kestrel endpoints configured above.
    builder.WebHost.UseNetTcp(options => {
        options.Listen("net.tcp://localhost:5002");
    });

    var app = builder.Build();

    var wsHttpBinding = new WSHttpBinding(SecurityMode.Transport);
    wsHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;

    ((IApplicationBuilder)app).UseServiceModel(option => {
        option
            .AddService<NorthwindWcfService>(serviceOptions => {
                // CoreWCF resolves relative endpoint addresses against these base addresses per binding scheme.
                // UseNetTcp only registers the net.tcp scheme, so we explicitly add http/https for CoreWCF endpoint resolution.
                // net.tcp base address is already registered by UseNetTcp(5002).
                serviceOptions.BaseAddresses.Add(new Uri("http://localhost:5000/"));
                serviceOptions.BaseAddresses.Add(new Uri("https://localhost:5001/"));
            })
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(new BasicHttpBinding(), "/NorthwindWcfService/basic")
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(wsHttpBinding, "/NorthwindWcfService/ws")
            .AddServiceEndpoint<NorthwindWcfService, INorthwindService>(new NetTcpBinding(SecurityMode.None), "/NorthwindWcfService/nettcp");
    });

    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpGetEnabled = true;

    Console.WriteLine("  BasicHttp  : http://localhost:5000/NorthwindWcfService/basic");
    Console.WriteLine("  WSHttp     : https://localhost:5001/NorthwindWcfService/ws");
    Console.WriteLine("  NetTcp     : net.tcp://localhost:5002/NorthwindWcfService/nettcp");

    await app.RunAsync();
    return 0;
}

async Task<int> RunGrpcAsync() {
    Console.WriteLine("Starting gRPC service...");

    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(options => {
        options.ListenAnyIP(5003, o => o.Protocols = HttpProtocols.Http2);
    });
    builder.Services.AddGrpc();

    var app = builder.Build();
    app.MapGrpcService<NorthwindGrpcService>();

    Console.WriteLine("  gRPC (HTTP/2) : http://localhost:5003");

    await app.RunAsync();
    return 0;
}