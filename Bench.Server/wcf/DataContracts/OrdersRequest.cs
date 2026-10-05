using System.Runtime.Serialization;

namespace Bench.wcf.DataContracts;

[DataContract]
public sealed class OrdersRequest {
    /// <summary>Number of orders to return. 0 or less means "use the server default" (10000).</summary>
    [DataMember] public int Count { get; set; }
}
