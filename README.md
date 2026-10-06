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
- For `GetOrders` the client allocates roughly 1.5 KB of managed heap per order, so those runs are
  allocation-bound long before they are transport-bound. Read the **Memory / GC** block of the
  report before comparing them. See [Benchmarking `GetOrders`](#benchmarking-getorders).
- **32 GB is not enough memory to run the whole `GetOrders` matrix.** `grpc` has a working-set cliff
  between `-c 10` and `-c 20`: 0.30 GB at `-c 10` against 19.6 GB at `-c 20`, and 19-23 GB above
  that. Halving the batch size barely helps, so those cells are recorded as **not measurable**
  rather than filled with numbers contaminated by swapping. See the
  [`GetOrders` findings](#batch-findings).
- Both processes use the default workstation GC. Server GC would scale better at high
  concurrency, but switching it would make the `GetOrderById` table above unreproducible with
  the documented commands, so it was left alone.

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
 
Measured payload for the 10,000 order batch on .NET 10:

| Transport                   | Serialized size |
|-----------------------------|--------------|
| `grpc` (protobuf)           | ~2.5 MB      |
| `basichttp` (SOAP 1.1 + XML) | ~11 MB      |
| `wsHttp` (SOAP 1.1 + XML)   | ~11 MB       |
| `nettcp`     | ~5.9 MB      |
 

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

Throughput at each concurrency level

![Throughput vs Concurrency](./docs/Chart1.png)


| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | **22,756** | 15,950 | 16,646 | 14,471 |
| 10 | **82,253** | 75,412 | 59,179 | 51,415 |
| 20 | **81,628** | 74,928 | 61,618 | 53,607 |
| 40 | **78,951** | 75,441 | 60,138 | 51,635 |
| 60 | **76,015** | 73,126 | 57,453 | 49,199 |
| 80 | **74,621** | 69,009 | 55,093 | 47,945 |
| 100 | **72,007** | 68,067 | 52,589 | 45,100 |


### Raw data

 **Zero failed calls across all 28 runs** (~97.0M successful requests). Latency in milliseconds.

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp` | 1 | 22,756 req/s | 0.040 | 0.058 | 0.064 | 0.084 | 0.044 | 6.861 |
| `grpc` | 1 | 15,950 req/s | 0.060 | 0.080 | 0.087 | 0.107 | 0.063 | 1.432 |
| `basichttp` | 1 | 16,646 req/s | 0.056 | 0.071 | 0.078 | 0.123 | 0.060 | 3.991 |
| `wshttp` | 1 | 14,471 req/s | 0.064 | 0.080 | 0.088 | 0.134 | 0.069 | 3.451 |
| `nettcp` | 10 | 82,253 req/s | 0.103 | 0.126 | 0.138 | 0.704 | 0.121 | 6.097 |
| `grpc` | 10 | 75,412 req/s | 0.122 | 0.146 | 0.156 | 0.700 | 0.132 | 2.853 |
| `basichttp` | 10 | 59,179 req/s | 0.128 | 0.161 | 0.191 | 1.118 | 0.169 | 13.779 |
| `wshttp` | 10 | 51,415 req/s | 0.145 | 0.187 | 0.272 | 1.200 | 0.194 | 12.460 |
| `nettcp` | 20 | 81,628 req/s | 0.204 | 0.259 | 0.750 | 1.077 | 0.245 | 45.296 |
| `grpc` | 20 | 74,928 req/s | 0.232 | 0.286 | 0.316 | 1.177 | 0.267 | 10.199 |
| `basichttp` | 20 | 61,618 req/s | 0.236 | 0.321 | 1.102 | 1.644 | 0.324 | 23.135 |
| `wshttp` | 20 | 53,607 req/s | 0.266 | 0.436 | 1.225 | 1.867 | 0.373 | 14.005 |
| `nettcp` | 40 | 78,951 req/s | 0.401 | 1.129 | 1.364 | 1.681 | 0.506 | 73.474 |
| `grpc` | 40 | 75,441 req/s | 0.458 | 0.607 | 1.290 | 1.890 | 0.530 | 10.730 |
| `basichttp` | 40 | 60,138 req/s | 0.459 | 1.562 | 1.784 | 2.798 | 0.665 | 19.049 |
| `wshttp` | 40 | 51,635 req/s | 0.529 | 1.764 | 1.989 | 3.260 | 0.774 | 49.500 |
| `nettcp` | 60 | 76,015 req/s | 0.610 | 1.653 | 1.867 | 2.387 | 0.789 | 78.272 |
| `grpc` | 60 | 73,126 req/s | 0.691 | 1.358 | 2.022 | 2.628 | 0.820 | 9.128 |
| `basichttp` | 60 | 57,453 req/s | 0.693 | 2.195 | 2.458 | 3.885 | 1.044 | 22.843 |
| `wshttp` | 60 | 49,199 req/s | 0.802 | 2.475 | 2.776 | 4.379 | 1.219 | 25.096 |
| `nettcp` | 80 | 74,621 req/s | 0.828 | 2.049 | 2.279 | 3.048 | 1.072 | 67.385 |
| `grpc` | 80 | 69,009 req/s | 0.940 | 2.198 | 2.714 | 3.588 | 1.159 | 17.431 |
| `basichttp` | 80 | 55,093 req/s | 0.954 | 2.871 | 3.232 | 4.881 | 1.452 | 30.341 |
| `wshttp` | 80 | 47,945 req/s | 1.115 | 3.118 | 3.597 | 5.224 | 1.668 | 33.714 |
| `nettcp` | 100 | 72,007 req/s | 1.056 | 2.556 | 2.850 | 3.819 | 1.389 | 54.765 |
| `grpc` | 100 | 68,067 req/s | 1.194 | 2.741 | 3.236 | 4.413 | 1.469 | 39.929 |
| `basichttp` | 100 | 52,589 req/s | 1.241 | 3.568 | 4.028 | 5.886 | 1.901 | 36.866 |
| `wshttp` | 100 | 45,100 req/s | 1.487 | 3.935 | 4.533 | 6.410 | 2.217 | 39.077 |


### Findings (take this with huge grain of salt)

1. **Perf ranking: `nettcp` > `grpc` > `basichttp` > `wshttp`** at every concurrency level **except
   `-c 1`**, where `basichttp` (16,646) edges out `grpc` (15,950). The two are within 5% of each
   other and sit at the same ~16k req/s, so the honest statement is that they are indistinguishable
   at concurrency 1: with no queue to amortise against, the SOAP/XML tax hides behind per-call
   overhead. `nettcp` wins over `basichttp` and `wshttp` as expected - binary over raw TCP beats
   SOAP/XML over http. `nettcp` also wins over `grpc` but barely. Both use binary data (protobuf for
   grpc). Could it be that HTTP2 added significant overhead compared to raw TCP? Not for
   multi-megabyte payloads - see the [`GetOrders` results](#getorders-results), where the ranking
   flips.

2. **Throughput rises sharply up to a concurrency of 10, then plateaus and slowly decays.** The
   peak is at `-c 10` (`nettcp` 82,253 req/s); by `-c 100` every transport has lost 10-15%. The
   plateau spans `-c 10`-`20`, so the exact peak level is inside run-to-run noise.

3. **Latency grows roughly linearly with concurrency while throughput flattens.** For `nettcp`, p50
   goes 0.040 -> 0.103 -> 0.204 ms as concurrency goes 1 -> 10 -> 20: a near-exact 5x step in
   latency for each 5x step in concurrency, which is the signature of a saturated server.

4. **`wshttp` is consistently last** on both throughput and latency at every level from 10 upwards.
   The SOAP 1.2 / WS-Addressing envelope plus TLS costs adds the most overhead.

5. **`nettcp` has by far the worst tail latency.** Its `max` is 45-78 ms from `-c 20` upwards while
   `grpc` stays under 18 ms across the entire sweep. Binary framing wins in the median and loses in
   the extreme tail.

### Concurrency (important)

`-c N` creates **N independent clients, for gRPC as well as WCF**. Each `GrpcPerf` owns its own
`GrpcChannel`, so N workers means N HTTP/2 connections.

## Benchmarking `GetOrders`

`-op getorders` switches the harness to the batch operation. The same `-w`, `-d` and `-c` knobs
apply, and the report gains a **Memory / GC** block on every run (including `getorderbyid`, where
it is the baseline that makes the batch cost legible).

```bash
Bench.Client -target grpc -op getorders -w 10 -d 60 -c 4
Bench.Client -target wcf  -arg nettcp -op getorders -n 20000 -w 10 -d 60 -c 8
```

Things worth knowing before you compare the numbers:

- **The client allocates about 1.5 KB of managed heap per order.** At the default 10,000 orders
  that is ~15 MB per call, per worker, on top of the server. Check the **Memory / GC** block
  first: a run showing hundreds of MB allocated and dozens of gen2 collections is
  allocation-bound, and its throughput is mostly a statement about the deserializer, not the
  wire. For reference, `grpc` at `-c 2` / 10,000 orders allocates ~690 MB in 3 s and
  `basichttp` allocates more, because it first has to build 11.6 MB of XML.
- **Latency includes full deserialization.** The harness times the whole call, which is the
  user-visible cost, but it is not a pure transport measurement.
- **A 10,000 order response is 2.6 MB over protobuf and 11.6 MB over SOAP 1.1.** The gRPC
  channel's receive limit is raised to `int.MaxValue` so a count sweep does not fail on gRPC
  while succeeding on WCF; the default 4 MB would break at roughly 15,000 orders.
- **The client asserts the response length.** It resolves the expected count with the same
  `OrderData.ResolveCount` the server uses, so a clamped or truncated response is counted as a
  failure instead of quietly producing a wrong throughput.
- **Start lower than you would for `GetOrderById`.** `-c 100` at 10,000 orders is roughly
  1.4 GB of client working set, and `-c 100` is where the single-order benchmark peaked. The
  client prints an advisory estimate above 1 GB, but it is only a guess; the report's Peak WS
  is the figure to trust.
- **Peak WS is the kernel's high-water mark for the process**, taken from
  `getrusage(RUSAGE_SELF).ru_maxrss`. It covers the whole process lifetime, so it includes
  start-up, JIT and the warm-up pass rather than the measured window alone. It was previously
  read from `Process.PeakWorkingSet64`, which returns 0 on macOS and so printed `Peak WS : 0 B`
  for every run; that property is correct on Windows and is still used there. Note the unit
  trap: `ru_maxrss` is bytes on the Darwin family but kilobytes on Linux.
- **Treat the working-set estimate above as optimistic.** Measured on the batch operation at
  1,000 orders, gRPC peaks at 92 MB at `-c 1`, 150-168 MB at `-c 10`, and then jumps to
  6-8 GB at `-c 20` — a cliff well past what `concurrency x orders x 1.5 KB` predicts. The
  advisory estimate will not warn you about that step.
- **The client sizes the thread pool from `-c` before starting the workers.** Each worker holds a
  pool thread across the synchronous part of its call, and the pool's hill-climbing injector only
  adds about one thread per 500 ms. Left at the default, `-c 100` on the batch operation produced a
  62-second p95 and a 60-second run that reported 84 seconds elapsed - the run was measuring the
  thread injector rather than the transport. `GetOrderById` is unaffected, since a 0.1 ms call never
  holds a thread long enough for the injector to matter.
- **Restart the server between batch runs.** The WCF server reached 3.40 GB after ~75 minutes of
  sustained benchmarking (GC heap growth and fragmentation, not the batch cache, which is only
  ~150 MB). A bloated server changes what the client is competing with, and because gRPC is swept
  first at each concurrency level it inherited the worst of it.

## `GetOrders` results

1,000 orders per call, `-w 10 -d 60`, same sweep as above. **Zero failed calls across the 23 runs
that fit in memory** (776,670 successful calls). `n/a` marks the five `grpc` cells that exhaust
this machine - see finding 3 below.

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 468 | **907** | 243 | 203 |
| 10 | 1,287 | **1,294** | 821 | 715 |
| 20 | **764** | n/a | 612 | 564 |
| 40 | **548** | n/a | 441 | 417 |
| 60 | **505** | n/a | 408 | 383 |
| 80 | **464** | n/a | 384 | 368 |
| 100 | **442** | n/a | 365 | 330 |

Throughput in calls/s; each call returns 1,000 orders, so multiply by 1,000 for orders/s.

### Raw data with memory

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max | Allocated | Peak WS |
|---|---|---|---|---|---|---|---|---|---|---|
| `nettcp` | 1 | 468 req/s | 1.998 | 2.701 | 2.740 | 2.936 | 2.137 | 4.050 | 42.6 GB | 0.10 GB |
| `grpc` | 1 | 907 req/s | 0.996 | 1.391 | 1.947 | 2.043 | 1.102 | 18.894 | 66.4 GB | 0.09 GB |
| `basichttp` | 1 | 243 req/s | 3.997 | 4.553 | 4.668 | 4.981 | 4.110 | 30.199 | 44.5 GB | 0.11 GB |
| `wshttp` | 1 | 203 req/s | 4.660 | 6.919 | 7.116 | 7.688 | 4.931 | 21.459 | 37.1 GB | 0.12 GB |
| `nettcp` | 10 | 1,287 req/s | 7.237 | 11.778 | 12.912 | 16.500 | 7.769 | 41.303 | 117.1 GB | 0.16 GB |
| `grpc` | 10 | 1,294 req/s | 6.864 | 14.122 | 16.627 | 21.787 | 7.725 | 35.210 | 94.8 GB | 0.30 GB |
| `basichttp` | 10 | 821 req/s | 11.680 | 17.799 | 19.332 | 22.885 | 12.186 | 34.553 | 149.9 GB | 0.26 GB |
| `wshttp` | 10 | 715 req/s | 13.486 | 20.076 | 21.809 | 26.194 | 13.977 | 41.623 | 130.9 GB | 0.25 GB |
| `nettcp` | 20 | 764 req/s | 22.512 | 44.976 | 55.177 | 74.139 | 26.177 | 134.827 | 69.5 GB | 0.47 GB |
| `grpc` | 20 | **not measurable** | | | | | | | | — | — |
| `basichttp` | 20 | 612 req/s | 29.409 | 54.029 | 65.088 | 83.093 | 32.681 | 142.247 | 111.8 GB | 0.69 GB |
| `wshttp` | 20 | 564 req/s | 32.764 | 56.253 | 65.584 | 81.304 | 35.440 | 123.405 | 103.2 GB | 0.68 GB |
| `nettcp` | 40 | 548 req/s | 61.491 | 139.312 | 169.611 | 229.104 | 72.959 | 353.332 | 49.9 GB | 1.28 GB |
| `grpc` | 40 | **not measurable** | | | | | | | | — | — |
| `basichttp` | 40 | 441 req/s | 79.193 | 164.476 | 193.955 | 252.551 | 90.610 | 424.616 | 80.7 GB | 1.39 GB |
| `wshttp` | 40 | 417 req/s | 84.287 | 169.796 | 199.447 | 256.952 | 95.750 | 411.279 | 76.5 GB | 1.42 GB |
| `nettcp` | 60 | 505 req/s | 98.649 | 238.889 | 286.350 | 375.793 | 118.764 | 858.652 | 46.0 GB | 1.22 GB |
| `grpc` | 60 | **not measurable** | | | | | | | | — | — |
| `basichttp` | 60 | 408 req/s | 129.353 | 274.292 | 316.910 | 403.411 | 147.000 | 646.182 | 74.6 GB | 1.29 GB |
| `wshttp` | 60 | 383 req/s | 138.326 | 288.253 | 339.537 | 433.581 | 156.541 | 835.753 | 70.3 GB | 2.63 GB |
| `nettcp` | 80 | 464 req/s | 141.973 | 355.656 | 424.242 | 545.885 | 172.472 | 923.853 | 42.2 GB | 1.26 GB |
| `grpc` | 80 | **not measurable** | | | | | | | | — | — |
| `basichttp` | 80 | 384 req/s | 183.925 | 402.884 | 467.299 | 600.399 | 208.051 | 999.284 | 70.3 GB | 1.96 GB |
| `wshttp` | 80 | 368 req/s | 194.376 | 406.046 | 469.866 | 590.918 | 217.232 | 933.886 | 67.4 GB | 1.47 GB |
| `nettcp` | 100 | 442 req/s | 183.169 | 479.509 | 578.293 | 776.474 | 226.297 | 1352.488 | 40.3 GB | 5.02 GB |
| `grpc` | 100 | **not measurable** | | | | | | | | — | — |
| `basichttp` | 100 | 365 req/s | 239.772 | 540.405 | 625.598 | 803.603 | 273.827 | 1315.095 | 66.8 GB | 1.75 GB |
| `wshttp` | 100 | 330 req/s | 270.641 | 568.275 | 659.834 | 830.096 | 302.762 | 1578.572 | 60.5 GB | 2.14 GB |

### Batch findings

1. **`grpc` is the fastest transport on the batch operation where it can be measured** - 907 req/s
   against `nettcp`'s 468 at `-c 1` (1.9x), and a dead heat at `-c 10` (1,294 vs 1,287). This is the
   opposite of the single-order result, where `nettcp` leads everywhere. Neither ranking generalises
   to the other operation.

2. **Throughput peaks at `-c 10` and then decays monotonically.** `nettcp` goes 468 -> 1,287 -> 764
   -> 442 as concurrency goes 1 -> 10 -> 20 -> 100, while p50 latency climbs 2.0 ms -> 7.2 ms ->
   183 ms. There is no plateau on this operation: more workers simply make every worker slower.

3. **`grpc` has a memory cliff between `-c 10` and `-c 20`, and batch size barely moves it.** At
   `-c 10` gRPC peaks at **0.30 GB**; at `-c 20` it needs **19.6 GB**, and 19-23 GB from `-c 40`
   upwards. Dropping the batch from 10,000 orders to 1,000 only took the ceiling from 24.6 GB to
   about 20 GB, so the driver is concurrency - 110 pool threads at `-c 100`, each with a live
   response - not the payload. Two retries at raised limits (26 then 29 GB of RSS, 2 then 5 GB of
   swap growth) both ended in the machine swapping and being killed. This is recorded as **not
   measurable**, not as "slow": the two cells that did complete under a raised limit reported p99
   latencies of 35.6 s and 37.7 s and overshot a 60-second run to 73 seconds, which measures the
   swap rather than the transport. More RAM, or Server GC, would very likely fill these cells in.

4. **For the three WCF transports, throughput per byte of memory is the story.** At `-c 100`,
   `nettcp` delivers 442 req/s on 5.02 GB while `basichttp` manages 365 req/s on 1.75 GB and
   `wshttp` 330 req/s on 2.14 GB. The SOAP transports allocate ~2.5x per order what the binary ones
   do, because they materialise ~11 MB of XML before a single `Order` object exists.

### Larger batch: 10,000 orders per call

The same sweep at the default 10,000 orders is where the memory ceiling in finding 3 becomes
unavoidable. Zero failed calls across the 22 runs that fit (62,842 successful calls):

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 41 | **61** | 23 | 21 |
| 10 | **79** | n/a | 59 | 55 |
| 20 | **69** | n/a | 51 | 48 |
| 40 | **55** | n/a | 46 | 44 |
| 60 | **55** | n/a | 42 | 40 |
| 80 | **50** | n/a | 40 | 38 |
| 100 | **45** | n/a | 39 | 38 |

The shape is the same - `nettcp` leads from `-c 10` up, throughput decays with concurrency - but the
gap widens and gRPC's ceiling moves down to `-c 1` instead of `-c 10`. `nettcp`'s working set at
`-c 100` doubles from 5.02 GB to 12.14 GB, and `basichttp`'s grows from 1.75 GB to 17.91 GB. Only the
throughput summary is reproduced here; the per-run latency and memory figures for this sweep were
measured under the same conditions but are not tabulated.

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

Three things to know before running it:

- **The WCF server must already be running** (`Bench.Server -target wcf`), since the WSDL is fetched
  over HTTP.
- **`-o NorthwindServiceClient.cs` is required.** Without it `svcutil` 8.0.0 names the output
  `Reference.cs` and leaves the real proxy stale.
- **`svcutil` refuses to overwrite an existing output file, and `-r` makes it build
  `Bench.Client` first** to resolve the shared DataContract types. So keep the old proxy in place
  while regenerating; deleting it first makes the project fail to compile (`WcfPerf` needs the
  proxy) and `svcutil` aborts. Move it aside, run the command, then replace it.

`-r` is what keeps the generated file free of duplicated DataContract types: `Order`, `Customer`,
`OrderDetail`, `OrderRequest`, `OrdersRequest` and `OrdersResponse` are linked in from
`Bench.Server` via
`<Compile Include>` and referenced by namespace. Any new DataContract on the server must be added
to that list, otherwise `svcutil` re-declares a private copy of it inside the proxy.

Note that `svcutil` also offers to inject `System.ServiceModel.Duplex` / `.Security` /
`.Federation` at floating `4.10.*` versions and to rewrite this file's formatting. Neither was
kept: the project builds without them, and floating versions work against the pinned
`10.0.652802` packages that the benchmark results depend on.

