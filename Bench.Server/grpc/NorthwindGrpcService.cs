using Google.Protobuf.Collections;
using Grpc.Core;
using Northwind;

namespace Bench.grpc;

public class NorthwindGrpcService : Northwind.NorthwindService.NorthwindServiceBase
{
    public override Task<Order> GetOrderById(OrderRequest request, ServerCallContext context)
    {
        return Task.FromResult(new Order() { Customer = new Customer()});
    }
}