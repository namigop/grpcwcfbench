using System.Collections.Concurrent;
using Bench.wcf.DataContracts;

namespace Bench.wcf;

// ReSharper disable once ClassNeverInstantiated.Global
public class NorthwindWcfService : INorthwindService {
    private static readonly ConcurrentDictionary<int, OrdersResponse> Orders = new(); 
    public Order GetOrderById(OrderRequest request) {
        return BuildOrder(0);
    }

    public OrdersResponse GetOrders(OrdersRequest request) {
        int count = OrderData.ResolveCount(request.Count);
        var orders = Orders.GetOrAdd(count, BuildOrdersResponse);
        return orders;
    }

    private static OrdersResponse BuildOrdersResponse(int count) {
        var orders = new Order[count];
        for (int i = 0; i < count; i++) {
            orders[i] = BuildOrder(i);
        }

        return new OrdersResponse { Orders = orders };
    }

    private static Order BuildOrder(int i) {
        int orderId = OrderData.OrderId(i);
        int customerId = OrderData.CustomerId(i);
        return new Order() {
            EmployeeId = OrderData.EmployeeId(i),
            Freight = OrderData.Freight(i),
            OrderDate = OrderData.OrderDate(i),
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
            RequiredDate = OrderData.RequiredDate(i),
            ShipAddress = OrderData.ShipAddress(i),
            ShipCountry = OrderData.ShipCountry(i),
            ShipCity = OrderData.ShipCity(i),
            ShipName = OrderData.ShipName(i),
            ShippedDate = OrderData.ShippedDate(i),
            ShipPostalCode = OrderData.ShipPostalCode(i),
            ShipRegion = OrderData.ShipRegion(i),
            ShipVia = OrderData.ShipVia(i),
            Status = OrderData.Status(i),
            CustomerId = customerId,
            Customer = new Customer() {
                CustomerId = customerId,
                Address = OrderData.Address(customerId),
                City = OrderData.City(customerId),
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