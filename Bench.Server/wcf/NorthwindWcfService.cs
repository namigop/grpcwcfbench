using Bench.wcf.DataContracts;

namespace Bench.wcf;

public class NorthwindWcfService : INorthwindService {
    public Order GetOrderById(OrderRequest request) {
        return new Order() { Customer = new Customer()};
    }
}

public interface INorthwindService {

    Order GetOrderById(OrderRequest request);
}