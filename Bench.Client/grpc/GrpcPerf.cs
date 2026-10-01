using Bench.Client;
using Grpc.Net.Client;

namespace Bench.Client.grpc;

/// <summary>
/// gRPC counterpart to <see cref="Bench.Client.wcf.Perf"/>. A single instance is shared by every
/// worker on purpose: HTTP/2 multiplexes concurrent calls over one connection, so one channel is
/// the idiomatic client shape. WCF needs one client per worker instead, so <c>-c N</c> means N
/// sockets for WCF but still one for gRPC.
/// </summary>
public sealed class GrpcPerf : IDisposable {
    private readonly GrpcChannel _channel;
    private readonly global::Northwind.NorthwindService.NorthwindServiceClient _client;

    // Reused across calls: a fresh request per operation would show up as allocation noise in
    // the numbers this harness exists to measure.
    private readonly global::Northwind.OrderRequest _request = new() { OrderId = 1 };

    public GrpcPerf(string address) {
        _channel = GrpcChannel.ForAddress(address);
        _client = new global::Northwind.NorthwindService.NorthwindServiceClient(_channel);
    }

    public async Task<bool> Call(int clientId, CancellationToken ct = default) {
        var order = await _client.GetOrderByIdAsync(_request);
        return order is not null;
    }

    public void Dispose() => _channel.Dispose();
}
