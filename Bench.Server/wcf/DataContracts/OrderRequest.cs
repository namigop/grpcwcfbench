using System.Runtime.Serialization;

namespace Bench.wcf.DataContracts;

[DataContract]
public sealed class OrderRequest {
    [DataMember] public int OrderId { get; set; }
}