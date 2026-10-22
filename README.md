# WCF vs gRPC Benchmark

Micro-benchmark comparing **WCF (CoreWCF) over three transports** against **gRPC** for two
unary methods, on .NET 10 / C#: a single-order `GetOrderById` and a batch `GetOrders`
(10,000 orders by default). Both operations are swept across the full concurrency range and both
result sets are published below.

This benchmark was done as a fun excercise. Be aware of the following caveats
- Both client and server were ran on the same machine.
- The response of the `GetOrderById` method is hardcoded.  There is no additional logic apart from just returning an `Order` instance
- No finetuning was done on the gRPC or WCF servers
- The duration of the benchmark is just 60 sec (with a 10 sec warmup time)
- For `GetOrders` the harness measures the whole call including deserialization, so the batch numbers
  are a statement about the deserializer as much as about the wire.
- **Both processes run Server GC**, set explicitly in each csproj. Neither project uses
  `Microsoft.NET.Sdk.Web`, which is what would normally turn Server GC on, so it has to be requested.
  Every number published here was measured with that configuration.
- The WCF net.tcp endpoint needs `MaxBufferSize` raised well above the 64 KB default or the batch
  operation fails outright. See [Notes on the harness](#notes-on-the-harness).

## Transports compared

| Target | Transport | Endpoint |
|---|---|---|
| `wcf` `basichttp` | CoreWCF `BasicHttpBinding` (SOAP 1.1) | `http://localhost:5000/NorthwindWcfService/basic` |
| `wcf` `wshttp` | CoreWCF `WSHttpBinding` (SOAP 1.2 over HTTPS) | `https://localhost:5001/NorthwindWcfService/ws` |
| `wcf` `nettcp` | CoreWCF `NetTcpBinding` (binary over raw TCP) | `net.tcp://localhost:5002/NorthwindWcfService/nettcp` |
| `grpc` | gRPC over HTTP/2 | `http://localhost:5003` |

## Service methods

Both services expose the same two operations, so the transports can be compared on a single-order
and a batch response.

| Method | gRPC | WCF | Returns |
|---|---|---|---|
| `GetOrderById` | `rpc GetOrderById(OrderRequest) returns (Order)` | `Order GetOrderById(OrderRequest)` | one hardcoded order |
| `GetOrders` | `rpc GetOrders(OrdersRequest) returns (OrdersResponse)` | `OrdersResponse GetOrders(OrdersRequest)` | **10,000** orders |

Both `GetOrders` operations return a response wrapper holding the batch, which mirrors the
`OrdersResponse` message on both sides and leaves room for paging metadata later:

```
WCF     OrdersResponse { Order[] Orders }
gRPC    OrdersResponse { repeated Order orders }
```

`GetOrders` takes an optional `Count` (gRPC) / `Count` (WCF `OrdersRequest`). A value of 0 or less
means "use the server default" of 10,000. Max count is clamped to 1,000,000. The batch is materialised once and cached per process, because
rebuilding 10,000 orders on every call would measure the allocator rather than the transport.
 
Measured response payload on .NET 10, straight off the wire:

| Transport | Encoding | **n = 1,000** | n = 10,000 | bytes/order | vs gRPC |
|---|---|---|---|---|---|
| `grpc` | protobuf | **263,626 B** (0.251 MB) | 2,638,546 B | 263.9 | 1.00x |
| `nettcp` | binary XML | **323,785 B** (0.309 MB) | 3,234,685 B | 323.4 | 1.23x |
| `basichttp` | SOAP 1.1 XML | **1,154,714 B** (1.101 MB) | 11,565,085 B | 1156.7 | 4.38x |
| `wshttp` | SOAP 1.2 + WS-Addressing | **1,154,948 B** (1.101 MB) | 11,565,319 B | 1156.7 | 4.38x |

SOAP is **4.4x more verbose than protobuf** and 3.6x more verbose than net.tcp's binary XML. Measured
four different ways: `curl` reports the exact HTTP body length for the two SOAP transports,
`CalculateSize()` gives the exact protobuf encoding for gRPC, and a byte-counting TCP proxy measures
the real net.tcp framing. All four are deterministic - repeated net.tcp runs are byte-identical.

> An earlier version of this table gave `nettcp` as ~5.9 MB. That was an estimate ("about half of
> SOAP"), never measured, and it was wrong by 1.9x. The 3.09 MB above is measured.

Two things are worth noticing. The two SOAP transports have an **identical** marginal cost -
1156.71 bytes per order either way, difference 0.0000 - because they share the same
`DataContract` XML and differ only in a 234-byte WS-Addressing/SOAP 1.2 envelope. And protobuf
carries essentially no per-message envelope at all, so its size is pure payload.
 

## Environment

- macOS 26.5.2, Apple M4, 10 cores, 32 GB
- .NET SDK 10.0.100, `net10.0`
- CoreWCF 1.9.1, gRPC 2.84.0, `System.ServiceModel.Http` 10.0.652802

## Test Setup
- server warmup time `10 sec` (done to exlude connection setup and JIT time)
- test duration `60 sec` 
- concurrency levels `1/10/20/40/60/80/100` (simulates the number of concurrent clients. Each client has its own channel)

## Results

All results in this section are `GetOrderById`. The batch results are in
[their own section](#getorders-results).

Throughput and p99 tail latency at each concurrency level

![GetOrderById throughput and p99 latency across 7 concurrency levels](./docs/getorderbyid.png)


| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | **22,269** | 17,501 | 19,775 | 17,189 |
| 10 | **84,837** | 79,872 | 70,989 | 63,376 |
| 20 | **88,913** | 85,869 | 74,575 | 65,315 |
| 40 | **91,338** | 88,643 | 77,064 | 68,520 |
| 60 | **93,129** | 89,527 | 79,398 | 68,814 |
| 80 | **92,360** | 89,344 | 78,705 | 68,149 |
| 100 | **92,811** | 88,898 | 78,944 | 67,180 |


### Raw data

**Zero failed calls across all 28 runs** (120,199,172 successful requests). Latency in milliseconds.

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp` | 1 | 22,269 req/s | 0.040 | 0.061 | 0.073 | 0.115 | 0.045 | 16.809 |
| `grpc` | 1 | 17,501 req/s | 0.053 | 0.078 | 0.090 | 0.121 | 0.057 | 9.159 |
| `basichttp` | 1 | 19,775 req/s | 0.044 | 0.064 | 0.078 | 0.136 | 0.050 | 11.365 |
| `wshttp` | 1 | 17,189 req/s | 0.051 | 0.071 | 0.087 | 0.154 | 0.058 | 9.971 |
| `nettcp` | 10 | 84,837 req/s | 0.108 | 0.137 | 0.154 | 0.211 | 0.118 | 15.134 |
| `grpc` | 10 | 79,872 req/s | 0.118 | 0.148 | 0.166 | 0.221 | 0.125 | 6.027 |
| `basichttp` | 10 | 70,989 req/s | 0.125 | 0.164 | 0.189 | 0.268 | 0.141 | 13.222 |
| `wshttp` | 10 | 63,376 req/s | 0.141 | 0.186 | 0.215 | 0.307 | 0.158 | 18.626 |
| `nettcp` | 20 | 88,913 req/s | 0.209 | 0.263 | 0.289 | 0.386 | 0.225 | 19.759 |
| `grpc` | 20 | 85,869 req/s | 0.224 | 0.278 | 0.300 | 0.370 | 0.233 | 23.133 |
| `basichttp` | 20 | 74,575 req/s | 0.240 | 0.309 | 0.351 | 0.565 | 0.268 | 15.511 |
| `wshttp` | 20 | 65,315 req/s | 0.275 | 0.353 | 0.403 | 0.645 | 0.306 | 17.063 |
| `nettcp` | 40 | 91,338 req/s | 0.406 | 0.514 | 0.562 | 1.625 | 0.438 | 40.025 |
| `grpc` | 40 | 88,643 req/s | 0.433 | 0.543 | 0.582 | 0.708 | 0.451 | 13.586 |
| `basichttp` | 40 | 77,064 req/s | 0.468 | 0.600 | 0.674 | 2.538 | 0.519 | 17.276 |
| `wshttp` | 40 | 68,520 req/s | 0.526 | 0.679 | 0.768 | 2.954 | 0.584 | 23.643 |
| `nettcp` | 60 | 93,129 req/s | 0.597 | 0.767 | 0.851 | 2.846 | 0.644 | 20.731 |
| `grpc` | 60 | 89,527 req/s | 0.642 | 0.816 | 0.877 | 1.222 | 0.670 | 23.829 |
| `basichttp` | 60 | 79,398 req/s | 0.691 | 0.887 | 0.989 | 2.789 | 0.756 | 75.156 |
| `wshttp` | 60 | 68,814 req/s | 0.786 | 1.021 | 1.168 | 4.312 | 0.872 | 18.166 |
| `nettcp` | 80 | 92,360 req/s | 0.802 | 1.045 | 1.177 | 3.667 | 0.866 | 23.311 |
| `grpc` | 80 | 89,344 req/s | 0.857 | 1.101 | 1.188 | 2.036 | 0.895 | 26.239 |
| `basichttp` | 80 | 78,705 req/s | 0.929 | 1.207 | 1.362 | 3.686 | 1.016 | 162.055 |
| `wshttp` | 80 | 68,149 req/s | 1.057 | 1.395 | 1.625 | 5.132 | 1.174 | 32.739 |
| `nettcp` | 100 | 92,811 req/s | 1.001 | 1.313 | 1.500 | 4.132 | 1.077 | 18.380 |
| `grpc` | 100 | 88,898 req/s | 1.079 | 1.388 | 1.499 | 3.297 | 1.125 | 23.223 |
| `basichttp` | 100 | 78,944 req/s | 1.161 | 1.514 | 1.703 | 4.462 | 1.266 | 36.820 |
| `wshttp` | 100 | 67,180 req/s | 1.344 | 1.785 | 2.154 | 5.795 | 1.488 | 22.903 |


### Findings (take this with huge grain of salt)

1. **Perf ranking: `nettcp` > `grpc` > `basichttp` > `wshttp`** at every concurrency level. `nettcp`
   wins over `basichttp` and `wshttp` as expected - binary over raw TCP beats SOAP/XML over http. It
   also wins over `grpc`, but only by 3.0-4.4% from `-c 20` upwards. Both use binary data (protobuf for
   grpc), so any HTTP/2 overhead over raw TCP is small and does not grow with payload size.

2. **At `-c 1` the two SOAP and binary HTTP paths are much closer than expected**, and `basichttp`
   (19,775) beats `grpc` (17,501) by 13%. With a single worker there is no queue to amortise against, so
   the SOAP/XML tax is hidden behind per-call overhead. `grpc` only pulls ahead from `-c 10` onwards.
   This is the one place where the ordering differs from every other row.

3. **Throughput rises steeply to `-c 10` and then plateaus.** From `-c 20` to `-c 100` every transport
   gains less than 6%: `nettcp` 88,913 -> 92,811 (+4.4%), `grpc` 85,869 -> 88,898 (+3.5%), `basichttp`
   +5.9%, `wshttp` +2.9%. Past about 20 workers the server is saturated and extra concurrency buys
   little; the SOAP transports even lose a little of their `-c 10` edge.

4. **Latency grows roughly linearly with concurrency while throughput flattens.** For `nettcp`, p50
   goes 0.040 -> 0.108 -> 0.209 -> 1.001 ms as concurrency goes 1 -> 10 -> 20 -> 100: close to an
   exact multiple, which is the signature of a saturated server. Throughput over the same range moves
   22,269 -> 92,811, only 4.2x for 100x the concurrency.

5. **`wshttp` is consistently last** on both throughput and latency at every level. The SOAP 1.2 /
   WS-Addressing envelope plus TLS adds the most overhead, and its p99 is the worst of the four at
   every level from `-c 10` upwards.

6. **`grpc` has the best tail latency once the server is loaded.** Its p99 is the lowest of the four
   from `-c 20` up - 3.297 ms at `-c 100` against `nettcp`'s 4.132, `basichttp`'s 4.462 and `wshttp`'s
   5.795 - and its `max` never exceeds 26.2 ms across the entire sweep, while `basichttp` reaches
   162.1 ms. At `-c 1` and `-c 10` there is no queue yet and `nettcp` still has the lowest p99, but
   once 20 workers are competing for the server, the one transport paying HTTP/2's framing cost is
   the one that still delivers the most predictable latency.

### Concurrency (important)

`-c N` creates **N independent clients, for gRPC as well as WCF**. Each `GrpcPerf` owns its own
`GrpcChannel`, so N workers means N HTTP/2 connections.

## Benchmarking `GetOrders`

`-op getorders` switches the harness to the batch operation. The same `-w`, `-d` and `-c` knobs
apply. The report format is identical to `GetOrderById` - throughput and latency only.

```bash
Bench.Client -target grpc -op getorders -w 10 -d 60 -c 4
Bench.Client -target wcf  -arg nettcp -op getorders -n 20000 -w 10 -d 60 -c 8
```

Things worth knowing before you compare the numbers:

- **Latency includes full deserialization.** The harness times the whole call, which is the
  user-visible cost, but it is not a pure transport measurement.
- **A 10,000 order response is 2.52 MB over protobuf, 3.09 MB over `nettcp` and 11.03 MB over either
  SOAP transport** - all four measured off the wire; see [Service methods](#service-methods). The gRPC
  channel's receive limit is raised to `int.MaxValue` so a count sweep does not fail on gRPC
  while succeeding on WCF; the default 4 MB would break at roughly 15,000 orders.
- **The client asserts the response length.** It resolves the expected count with the same
  `OrderData.ResolveCount` the server uses, so a clamped or truncated response is counted as a
  failure instead of quietly producing a wrong throughput.
- **The client sizes the thread pool from `-c` before starting the workers.** Each worker holds a
  pool thread across the synchronous part of its call, and the pool's hill-climbing injector only
  adds about one thread per 500 ms. Left at the default, `-c 100` on the batch operation produced a
  62-second p95 and a 60-second run that reported 84 seconds elapsed - the run was measuring the
  thread injector rather than the transport. `GetOrderById` is unaffected, since a 0.1 ms call never
  holds a thread long enough for the injector to matter.

 
## `GetOrders` results

1,000 orders per call, `-w 10 -d 60`, same sweep as above. **Zero failed calls across all 28 runs**
(3,210,299 successful calls).

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 445 | **919** | 233 | 213 |
| 10 | 1,800 | **4,914** | 1,009 | 818 |
| 20 | 1,925 | **5,180** | 1,054 | 839 |
| 40 | 1,969 | **4,971** | 1,059 | 854 |
| 60 | 1,982 | **4,763** | 1,043 | 852 |
| 80 | 1,947 | **4,597** | 1,047 | 850 |
| 100 | 1,957 | **4,324** | 1,067 | 854 |

Throughput in calls/s; each call returns 1,000 orders, so multiply by 1,000 for orders/s.

### Raw data

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp` | 1 | 445 req/s | 2.136 | 2.288 | 2.458 | 4.534 | 2.248 | 18.559 |
| `grpc` | 1 | 919 req/s | 1.048 | 1.154 | 1.239 | 1.956 | 1.088 | 15.389 |
| `basichttp` | 1 | 233 req/s | 4.092 | 5.101 | 5.496 | 6.779 | 4.301 | 17.965 |
| `wshttp` | 1 | 213 req/s | 4.438 | 5.670 | 6.074 | 7.322 | 4.691 | 28.357 |
| `nettcp` | 10 | 1,800 req/s | 4.740 | 8.870 | 10.630 | 15.090 | 5.554 | 64.339 |
| `grpc` | 10 | 4,914 req/s | 1.869 | 2.508 | 3.017 | 5.766 | 2.035 | 104.749 |
| `basichttp` | 10 | 1,009 req/s | 9.085 | 14.452 | 16.395 | 21.009 | 9.904 | 71.320 |
| `wshttp` | 10 | 818 req/s | 11.425 | 17.362 | 19.682 | 26.314 | 12.222 | 68.931 |
| `nettcp` | 20 | 1,925 req/s | 9.114 | 16.826 | 20.495 | 30.875 | 10.391 | 119.081 |
| `grpc` | 20 | 5,180 req/s | 3.556 | 5.273 | 6.119 | 9.474 | 3.861 | 32.353 |
| `basichttp` | 20 | 1,054 req/s | 16.909 | 30.229 | 36.342 | 53.768 | 18.970 | 132.278 |
| `wshttp` | 20 | 839 req/s | 21.583 | 36.698 | 43.263 | 60.925 | 23.838 | 170.396 |
| `nettcp` | 40 | 1,969 req/s | 18.277 | 32.234 | 39.443 | 60.253 | 20.318 | 170.086 |
| `grpc` | 40 | 4,971 req/s | 7.026 | 12.255 | 14.110 | 23.252 | 8.047 | 45.110 |
| `basichttp` | 40 | 1,059 req/s | 33.386 | 61.327 | 76.077 | 113.012 | 37.743 | 299.726 |
| `wshttp` | 40 | 854 req/s | 43.822 | 69.546 | 81.292 | 112.891 | 46.814 | 234.820 |
| `nettcp` | 60 | 1,982 req/s | 27.262 | 47.736 | 57.219 | 87.522 | 30.268 | 252.046 |
| `grpc` | 60 | 4,763 req/s | 10.720 | 19.683 | 23.770 | 36.944 | 12.597 | 60.956 |
| `basichttp` | 60 | 1,043 req/s | 51.465 | 91.972 | 111.965 | 164.545 | 57.522 | 359.591 |
| `wshttp` | 60 | 852 req/s | 67.102 | 102.088 | 117.530 | 157.513 | 70.359 | 328.733 |
| `nettcp` | 80 | 1,947 req/s | 37.330 | 64.529 | 77.773 | 114.437 | 41.061 | 256.861 |
| `grpc` | 80 | 4,597 req/s | 14.791 | 27.309 | 34.450 | 48.456 | 17.400 | 77.749 |
| `basichttp` | 80 | 1,047 req/s | 71.844 | 112.958 | 131.631 | 182.330 | 76.337 | 386.171 |
| `wshttp` | 80 | 850 req/s | 89.498 | 135.957 | 156.049 | 204.722 | 94.046 | 408.195 |
| `nettcp` | 100 | 1,957 req/s | 46.552 | 79.266 | 94.551 | 135.603 | 51.078 | 330.016 |
| `grpc` | 100 | 4,324 req/s | 19.889 | 36.300 | 43.910 | 60.011 | 23.114 | 106.532 |
| `basichttp` | 100 | 1,067 req/s | 88.216 | 139.165 | 160.951 | 213.699 | 93.692 | 413.856 |
| `wshttp` | 100 | 854 req/s | 113.656 | 164.910 | 185.014 | 236.020 | 117.055 | 416.081 |

### Batch findings

1. **`grpc` wins this operation at every concurrency level.** 919 req/s against `nettcp`'s 445 at
   `-c 1` (2.1x), 5,180 against 1,925 at the `-c 20` peak (2.7x), and 4,324 against 1,957 at `-c 100`
   (2.2x). Against the SOAP transports the margin is much wider: at `-c 100`, `basichttp` manages
   1,067 and `wshttp` 854, so gRPC is 4.1x and 5.1x.

2. **The ranking flips between the two operations, and payload size is why.** On `GetOrderById`,
   `nettcp` leads gRPC at every level - by 27% at `-c 1`, 6.2% at `-c 10`, and a steady 3.0-4.4% from
   `-c 20` upwards. Here gRPC leads `nettcp` by 2.2-2.7x. A single-order response is small enough that
   per-call overhead is the entire cost, and raw TCP's lower fixed cost wins. At 1,000 orders the
   response dominates and per-call overhead stops mattering; what is left is serialization, framing and
   buffering throughput, and there protobuf over HTTP/2 pulls away. Neither transport is "faster" in
   the abstract - the winner is a function of response size.

3. **Payload size accounts for most of the gap, and `nettcp`'s 2nd place is partly borrowed.**
   Measured response bytes are in [Service methods](#service-methods): at 1,000 orders protobuf
   carries 264 KB, net.tcp binary XML 324 KB, and either SOAP transport 1.15 MB. Multiplying payload
   by measured throughput at the `-c 20` peak gives the rate at which each transport actually moves
   data:

   | Transport | req/s @ c=20 | Payload | Payload moved |
   |---|---|---|---|
   | `grpc` | 5,180 | 263,626 B | **1.37 GB/s** |
   | `nettcp` | 1,925 | 323,785 B | 0.62 GB/s |
   | `basichttp` | 1,054 | 1,154,714 B | **1.22 GB/s** |
   | `wshttp` | 839 | 1,154,948 B | 0.97 GB/s |

   Two things fall out. gRPC wins on *both* axes, which the req/s table alone does not show. And
   `basichttp` moves **twice as many bytes per second as `nettcp`** despite being a little over half
   its throughput - its 4.4x larger payload is very nearly cancelled by a much faster serializer,
   while net.tcp's compact binary XML does not buy it a faster encoder. `nettcp`'s 2nd place is
   therefore partly a smaller payload rather than purely a faster stack.

4. **gRPC is the only transport that loses throughput as concurrency rises.** It peaks at `-c 20`
   with 5,180 req/s and decays 17% to 4,324 by `-c 100`, while the three WCF transports hold a flat
   plateau over the same range, all ending slightly *above* their own c=20 value: `nettcp` 1,925 ->
   1,957, `basichttp` 1,054 -> 1,067, `wshttp` 839 -> 854. Past about 20 workers every response is
   large enough that the server's serializer, not the queue, is the ceiling - extra concurrency only
   deepens the backlog.

5. **gRPC has the best tail latency at every level.** At `-c 100` its p99 is 60.0 ms against
   `nettcp`'s 135.6, `basichttp`'s 213.7 and `wshttp`'s 236.0. Its `max` never exceeds 106.5 ms
   anywhere in the sweep, while `basichttp` reaches 413.9 ms and `wshttp` 416.1 ms.


### Larger batch: 10,000 orders per call

This sweep is **not published any more**. It was measured on a workstation-GC client, and the
configuration it was measured under is no longer the configuration in this repository - both projects
now set `<ServerGarbageCollection>true</ServerGarbageCollection>`. Under Server GC every transport
completes the full sweep, so the `n/a` cells that made that table interesting no longer describe
anything real. Re-run it to regenerate:

```bash
./Bench.Client/bin/Release/net10.0/Bench.Client -target grpc -op getorders -w 10 -d 60 -c 20
```

The 10,000-order response is 2.52 MB over protobuf, 3.09 MB over `nettcp` and 11.03 MB over either
SOAP transport, so `MaxBufferSize` has to stay well clear of the 64 KB default - see
[Notes on the harness](#notes-on-the-harness).

## Notes on the harness

Three changes were made while benchmarking. Each one invalidates results measured before it, which is
why the numbers here are not comparable with any earlier version of this table.

1. **Server GC on both processes.** `Bench.Server` and `Bench.Client` both set
   `<ServerGarbageCollection>true</ServerGarbageCollection>` explicitly. Neither uses
   `Microsoft.NET.Sdk.Web`, which is what normally turns Server GC on, so it has to be requested.
   Results measured before this change are not comparable with the ones published here.
2. **The client sizes the thread pool from `-c` before starting the workers**
   (`Harness.EnsureThreadPoolCapacity`, floor of `concurrency + ProcessorCount`). Each worker holds a
   pool thread across the synchronous part of its call and the pool's hill-climbing injector adds only
   about one thread per 500 ms, so at `-c 100` the harness was measuring the injector: a 62-second p95
   and a 60-second run reporting 84 seconds elapsed. `GetOrderById` is unaffected, since a 0.1 ms call
   never holds a thread long enough to matter.
3. **The net.tcp binding raises `MaxBufferSize` to 16 MB.** The 64 KB default caps the session's
   *send* path as well as its receive path, so a 1,000-order response (324 KB) fails deterministically
   with `CommunicationException: The server did not provide a meaningful reply`. 64 KB is enough for
   `GetOrderById`, which is why this only ever showed up on the batch operation. The 16 MB bound is
   chosen to clear the 3.09 MB `nettcp` response at 10,000 orders with headroom, while staying bounded.

## Running

Build in Release mode

```bash
dotnet build -c Release
```

Start a server (one target per process):

```bash
Bench.Server -target wcf    # Single service instance reacheable BasicHttp :5000, WSHttp :5001, NetTcp :5002
Bench.Server -target grpc   # gRPC on HTTP/2 :5003
```

Then the client

```bash
# Single-call smoke test to see if th server is up (omit -w/-d/-c)
Bench.Client -target wcf -arg basichttp

# Benchmark
Bench.Client -target wcf  -arg basichttp -w 10 -d 60 -c 1
Bench.Client -target wcf  -arg wshttp    -w 10 -d 60 -c 20
Bench.Client -target wcf  -arg nettcp    -w 10 -d 60 -c 100
Bench.Client -target grpc                -w 10 -d 60 -c 100

# Batch operation (10,000 orders by default)
Bench.Client -target grpc                -op getorders -w 10 -d 60 -c 4
Bench.Client -target wcf  -arg basichttp -op getorders -w 10 -d 60 -c 4

# Batch operation at a specific size
Bench.Client -target grpc -op getorders -n 500 -w 10 -d 60 -c 8

# Override the endpoint
Bench.Client -target wcf -arg nettcp -address "net.tcp://localhost:5002/NorthwindWcfService/nettcp" -d 60
```

`-op` selects the operation and defaults to `getorderbyid`, so every command in the results
table above still behaves exactly as it did. `-n` sets the batch size and is only accepted
alongside `-op getorders`.

## Regenerating the chart

The chart in [Results](#results) is generated from the same TSV the tables are built from, so
the image cannot drift from the measured data:

```bash
python3 tools/gen-chart.py <results.tsv> --html /tmp/chart.html --png docs/getorderbyid.png
```

The TSV is tab separated, one row per run, with the columns
`op transport conc success failed elapsed_s throughput min_ms mean_ms p50_ms p90_ms p95_ms p99_ms max_ms`.
The script reads every number it plots out of that file, so nothing in the chart is typed by hand.


## Regenerating the WCF client

The WCF proxy in `Bench.Client/wcf/ServiceReference/` is generated from the running server:

```bash
cd Bench.Client
dotnet-svcutil http://localhost:5000/NorthwindWcfService/basic?wsdl \
  -d wcf/ServiceReference \
  -o NorthwindServiceClient.cs \
  -n 'http://tempuri.org/,Bench.Client.Wcf' \
  -ct System.Collections.Generic.List`1 \
  -ser DataContractSerializer \
  -r Bench.Client.csproj
```
 