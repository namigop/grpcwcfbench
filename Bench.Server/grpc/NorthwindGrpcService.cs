using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Northwind;

namespace Bench.grpc;

public class NorthwindGrpcService : Northwind.NorthwindService.NorthwindServiceBase {
    public override Task<Order> GetOrderById(OrderRequest request, ServerCallContext context) {
        return Task.FromResult(new Order() {
            EmployeeId = 1,
            Freight = 42.0,
            OrderDate = Timestamp.FromDateTime(DateTime.UtcNow.Date),
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
            RequiredDate =  Timestamp.FromDateTime(DateTime.UtcNow.Date),
            ShipAddress = "123 Main St",
            ShipCountry = "Singapore",
            ShipCity = "Singapore",
            ShipName = "HMS Car",
            ShippedDate = Timestamp.FromDateTime(DateTime.UtcNow.Date),
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
        });
    }
}