using System.Collections.Concurrent;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Northwind;

namespace Bench.grpc;

public class NorthwindGrpcService : Northwind.NorthwindService.NorthwindServiceBase {
    private static readonly ConcurrentDictionary<int, OrdersResponse> Orders = new(); 
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

    public override Task<OrdersResponse> GetOrders(OrdersRequest request, ServerCallContext context) {
        int count = OrderData.ResolveCount(request.Count);
        var orders = Orders.GetOrAdd(count, BuildOrdersResponse);
        return Task.FromResult(orders);
    }

    private static OrdersResponse BuildOrdersResponse(int count) {
        var response = new OrdersResponse();
        response.Orders.AddRange(BuildOrders(count));
        return response;
    }

    private static Order[] BuildOrders(int count) {
        var orders = new Order[count];
        for (int i = 0; i < count; i++) {
            orders[i] = BuildOrder(i);
        }

        return orders;
    }

    private static Order BuildOrder(int i) {
        int orderId = OrderData.OrderId(i);
        int customerId = OrderData.CustomerId(i);
        return new Order {
            EmployeeId = OrderData.EmployeeId(i),
            Freight = OrderData.Freight(i),
            OrderDate = Timestamp.FromDateTime(OrderData.OrderDate(i)),
            OrderId = orderId,
            OrderDetails = {
                new OrderDetail() {
                    OrderId = orderId,
                    ProductId = OrderData.ProductId(i),
                    Quantity = OrderData.Quantity(i),
                    UnitPrice = OrderData.UnitPrice(i),
                    Discount = OrderData.Discount(i)
                },
            },
            RequiredDate = Timestamp.FromDateTime(OrderData.RequiredDate(i)),
            ShipAddress = OrderData.ShipAddress(i),
            ShipCountry = OrderData.ShipCountry(i),
            ShipCity = OrderData.ShipCity(i),
            ShipName = OrderData.ShipName(i),
            ShippedDate = Timestamp.FromDateTime(OrderData.ShippedDate(i)),
            ShipPostalCode = OrderData.ShipPostalCode(i),
            ShipRegion = OrderData.ShipRegion(i),
            ShipVia = OrderData.ShipVia(i),
            Status = OrderData.Status(i),
            CustomerId = customerId,
            Customer = new Customer() {
                CustomerId = customerId,
                Address = OrderData.Address(customerId),
                City = OrderData.City(customerId),
                Region = OrderData.Region(customerId),
                PostalCode = OrderData.PostalCode(customerId),
                Country = OrderData.Country(customerId),
                CompanyName = OrderData.CompanyName(customerId),
                ContactName = OrderData.ContactName(customerId),
                ContactTitle = OrderData.ContactTitle(customerId),
                Fax = OrderData.Fax(customerId),
                Phone = OrderData.Phone(customerId)
            }
        };
    }
}