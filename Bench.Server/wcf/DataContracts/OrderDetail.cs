using System.Runtime.Serialization;

namespace Bench.wcf.DataContracts;

[DataContract]
 public sealed class OrderDetail {
    [DataMember]  public double Discount { get; set; }
    [DataMember]  public int OrderId { get; set; }
    [DataMember]  public int ProductId { get; set; }
    [DataMember]  public int Quantity { get; set; }
    [DataMember]  public double UnitPrice { get; set; }
}