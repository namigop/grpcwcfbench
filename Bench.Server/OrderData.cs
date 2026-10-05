using System.Collections.Concurrent;

namespace Bench;
 
public static class OrderData {
    public const int DefaultOrderCount = 10_000;
    public const int MaxOrderCount = 1_000_000;

    static readonly DateTime Epoch = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    static readonly string[] ShipNames = [
        "HMS Car", "HMS Cardinal", "HMS Cygnet", "HMS Daffodil", "HMS Endurance",
        "HMS Falcon", "HMS Gannet", "HMS Harrier", "HMS Ibis", "HMS Kestrel"
    ];

    static readonly string[] Companies = [
        "HMS Corporation", "Northwind Trading", "Contoso Ltd", "Tailspin Toys",
        "Litware Inc", "Adventure Works", "Wingtip Toys", "Proseware Inc",
        "Lucerne Publishing", "Fourth Coffee"
    ];

    static readonly string[] Contacts = [
        "John Doe", "Jane Smith", "Ana Trujillo", "Antonio Moreno", "Thomas Hardy",
        "Hans Berg", "Jmo Kyrkko", "Elizabeth Lincoln", "Peter Chong", "Ann Devon"
    ];

    static readonly string[] Titles = [
        "SGT", "Sales Manager", "Owner", "Accounting Manager", "Purchasing Manager",
        "Sales Representative", "Marketing Manager", "President"
    ];

    static readonly string[] Streets = [
        "123 Main St", "9 Queen St", "311 Sycamore Rd", "23 Tsawassen Blvd",
        "1 Rue Bagatelle", "91 Hanover Sq", "17 Lenin Ave", "Av. Paulista 1470",
        "Carrera de la Defensa 100", "ul. Konopnickiej 12"
    ];

    static readonly string[] Cities = [
        "Singapore", "Seattle", "São Paulo", "London", "Paris", "Helsinki",
        "Lisbon", "Mexico D.F.", "Berlin", "Bergen", "Lille", "Buenos Aires"
    ];

    static readonly string[] Regions = ["SEA", "WA", "SP", "Europe", "Nordics", "SA"];

    static readonly string[] Countries = [
        "Singapore", "USA", "Brazil", "UK", "France", "Finland", "Portugal", "Germany"
    ];

    static readonly string[] Statuses = ["Shipped", "Pending", "Processing", "New"];

    /// <summary>
    /// Resolves a caller supplied count. Anything non-positive means "the default 10,000", and
    /// the result is clamped so a fat fingered value cannot turn into an out of memory crash.
    /// </summary>
    public static int ResolveCount(int requested)
        => requested <= 0 ? DefaultOrderCount : Math.Min(requested, MaxOrderCount);

    // Order level fields, keyed by the 0-based order index.
    public static int OrderId(int i) => i + 1;
    public static int CustomerId(int i) => i % 100 + 1;
    public static int EmployeeId(int i) => i % 9 + 1;
    public static int ShipVia(int i) => i % 3 + 1;
    public static double Freight(int i) => 10.35 + (i % 500) * 0.73;
    public static DateTime OrderDate(int i) => Epoch.AddDays(i % 365);
    public static DateTime RequiredDate(int i) => Epoch.AddDays(i % 365).AddDays(7);
    public static DateTime ShippedDate(int i) => Epoch.AddDays(i % 365).AddDays(3);
    public static string Status(int i) => Statuses[i % Statuses.Length];
    public static string ShipName(int i) => ShipNames[i % ShipNames.Length];
    public static string ShipAddress(int i) => Streets[i % Streets.Length];
    public static string ShipCity(int i) => Cities[i % Cities.Length];
    public static string ShipRegion(int i) => Regions[i % Regions.Length];
    public static string ShipPostalCode(int i) => $"{i % 100000:00000}";
    public static string ShipCountry(int i) => Countries[i % Countries.Length];

    // Order detail fields, keyed by the same 0-based order index.
    public static int ProductId(int i) => i % 40 + 1;
    public static int Quantity(int i) => i % 20 + 1;
    public static double UnitPrice(int i) => 15.5 + (i % 300) * 1.37;
    public static double Discount(int i) => (i % 4) * 0.05;

    // Customer fields are keyed by customer id rather than by order index: an id repeats across
    // 100 orders, and the same id must always describe the same customer.
    public static string CompanyName(int customerId) => Companies[customerId % Companies.Length];
    public static string ContactName(int customerId) => Contacts[customerId % Contacts.Length];
    public static string ContactTitle(int customerId) => Titles[customerId % Titles.Length];
    public static string Address(int customerId) => Streets[customerId % Streets.Length];
    public static string City(int customerId) => Cities[customerId % Cities.Length];
    public static string Region(int customerId) => Regions[customerId % Regions.Length];
    public static string PostalCode(int customerId) => $"{customerId % 100000:00000}";
    public static string Country(int customerId) => Countries[customerId % Countries.Length];
    public static string Phone(int customerId) => $"+{customerId % 90 + 1} {(customerId % 700) + 300:000} {customerId % 1000:0000}";
    public static string Fax(int customerId) => $"+{customerId % 90 + 1} {(customerId % 700) + 400:000} {customerId % 1000:0000}";
    
}
