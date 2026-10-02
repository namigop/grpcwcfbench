using System.Runtime.Serialization;

namespace Bench.wcf.DataContracts;

[DataContract]
public sealed class Order {
    [DataMember] public Customer? Customer { get; set; }
    [DataMember]  public int CustomerId { get; set; }
    [DataMember]  public int EmployeeId { get; set; }
    [DataMember]  public double Freight { get; set; }
    [DataMember]  public DateTime OrderDate { get; set; }
    [DataMember] public List<OrderDetail> OrderDetails { get; set; } = [];
    [DataMember]  public int OrderId { get; set; }
    [DataMember]  public DateTime RequiredDate { get; set; }
    [DataMember]  public string ShipAddress { get; set; } = "";
    [DataMember]  public string ShipCity { get; set; } = "";
    [DataMember]  public string ShipCountry { get; set; } = "";
    [DataMember]  public string ShipName { get; set; } = "";
    [DataMember]  public string ShipPostalCode { get; set; } = "";
    [DataMember]  public string ShipRegion { get; set; } = "";
    [DataMember]  public int ShipVia { get; set; }
    [DataMember]  public DateTime ShippedDate { get; set; }
    [DataMember]  public string Status { get; set; } = "";
}