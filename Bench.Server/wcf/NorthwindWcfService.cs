using Bench.wcf.DataContracts;
using CoreWCF;

namespace Bench.wcf;

public class NorthwindWcfService : INorthwindService {
    public Order GetOrderById(OrderRequest request) {
        return new Order() { Customer = new Customer()};
    }
}

[ServiceContract]
public interface INorthwindService {

    [OperationContract]
    public Order GetOrderById(OrderRequest request);
}