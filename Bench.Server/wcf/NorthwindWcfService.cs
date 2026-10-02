using Bench.wcf.DataContracts;
using CoreWCF;

namespace Bench.wcf;

// ReSharper disable once ClassNeverInstantiated.Global
public class NorthwindWcfService : INorthwindService {
    public Order GetOrderById(OrderRequest request) {
        return new Order() {
            EmployeeId = 1,
            Freight = 42.0,
            OrderDate = DateTime.Today,
            OrderId = 1,
            OrderDetails = {
                new OrderDetail()
                {
                    ProductId = 1,
                    Quantity = 5,
                    UnitPrice = 123.45,
                    Discount = 0.5
                },
            },
            RequiredDate =  DateTime.Today,
            ShipAddress = "123 Main St",
            ShipCountry = "Singapore",
            ShipCity = "Singapore",
            ShipName = "HMS Car",
            ShippedDate = DateTime.Today,
            ShipPostalCode = "S91919",
            ShipRegion = "SEA",
            ShipVia = 1,
            CustomerId = 1,
            Customer = new Customer() {
                CustomerId = 1,
                Address = "123 Main St",
                City = "Singapore",
                Country = "Singapore",
                CompanyName = "HMS Corporation",
                ContactName = "John Doe",
                ContactTitle = "SGT",
                Fax = "999-478-673",
                Phone = "646-433-90"
            }
        };
    }
}

[ServiceContract]
public interface INorthwindService {

    [OperationContract]
    public Order GetOrderById(OrderRequest request);
}