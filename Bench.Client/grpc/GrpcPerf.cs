using Grpc.Net.Client;

namespace Bench.Client.grpc;

public sealed class GrpcPerf : IDisposable {
    private readonly GrpcChannel _channel;
    private readonly Northwind.NorthwindService.NorthwindServiceClient _client;

    // Reused across calls: a fresh request per operation would show up as allocation noise in
    // the numbers this harness exists to measure.
    private readonly Northwind.OrderRequest _request = new() { OrderId = 1 };

    public GrpcPerf(string address) {
        _channel = GrpcChannel.ForAddress(address);
        _client = new Northwind.NorthwindService.NorthwindServiceClient(_channel);
    }

    public async Task<bool> Call(int clientId, CancellationToken ct = default) {
        var order = await _client.GetOrderByIdAsync(_request);
        return order is not null;
    }

    public void Dispose() => _channel.Dispose();
}
