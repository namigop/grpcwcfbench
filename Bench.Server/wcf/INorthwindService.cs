using Bench.wcf.DataContracts;
using CoreWCF;

namespace Bench.wcf;

[ServiceContract]
public interface INorthwindService {

    [OperationContract]
    public Order GetOrderById(OrderRequest request);

    [OperationContract]
    public OrdersResponse GetOrders(OrdersRequest request);
}