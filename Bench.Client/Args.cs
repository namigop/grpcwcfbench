namespace Bench.Client;

public enum BenchOperation {
    GetOrderById,
    GetOrders
}

public record Args(
    string Address,
    int Concurrency,
    TimeSpan Duration,
    TimeSpan Warmup,
    BenchOperation Operation = BenchOperation.GetOrderById,
    int Count = 0) {

    /// <summary>
    /// Effective order count for <see cref="BenchOperation.GetOrders"/>. Resolved with the very
    /// same function the server applies, so the client can assert the response length without
    /// the two sides drifting apart. 0 means "the server default", which is 10,000.
    /// </summary>
    public int EffectiveOrderCount => OrderData.ResolveCount(Count);
}
