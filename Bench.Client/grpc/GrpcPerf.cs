using Grpc.Net.Client;

namespace Bench.Client.grpc;

public sealed class GrpcPerf : IDisposable {
    private readonly GrpcChannel _channel;
    private readonly Northwind.NorthwindService.NorthwindServiceClient _client;
    private readonly BenchOperation _operation;
    private readonly int _expectedOrders;
    private readonly Northwind.OrdersRequest? _ordersRequest;

    private readonly Northwind.OrderRequest _request = new() { OrderId = 1 };
    public int? LastOrders { get; private set; }

    public GrpcPerf(string address, BenchOperation operation, int count) {
        // The default 4 MB receive limit is under the size of a 10,000 order response, and the WCF
        // side already accepts int.MaxValue. Leaving the gRPC default in place would mean a count
        // sweep fails on gRPC and succeeds on WCF, which would be a bogus comparison.
        _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions { MaxReceiveMessageSize = int.MaxValue });
        _client = new Northwind.NorthwindService.NorthwindServiceClient(_channel);
        _operation = operation;

        if (operation == BenchOperation.GetOrders) {
            _expectedOrders = OrderData.ResolveCount(count);
            _ordersRequest = new Northwind.OrdersRequest { Count = count };
        }
    }

    public async Task<bool> Call(int clientId, CancellationToken ct = default) {
        switch (_operation) {
            case BenchOperation.GetOrderById:
                return await _client.GetOrderByIdAsync(_request) is not null;

            default: {
                var response = await _client.GetOrdersAsync(_ordersRequest!);
                LastOrders = response.Orders.Count;
                return LastOrders.Value == _expectedOrders;
            }
        }
    }

    public void Dispose() => _channel.Dispose();
}
