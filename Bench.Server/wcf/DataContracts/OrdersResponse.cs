using System.Runtime.Serialization;

namespace Bench.wcf.DataContracts;

/// <summary>
/// Response wrapper for <c>GetOrders</c>. It mirrors the gRPC <c>OrdersResponse</c> message so both
/// transports expose the same shape, and leaves room to add paging metadata later without breaking
/// the operation signature.
/// </summary>
[DataContract]
public sealed class OrdersResponse {
    [DataMember] public Order[] Orders { get; set; } = [];
}
