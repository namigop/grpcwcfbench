# WCF vs gRPC Benchmark

Micro-benchmark comparing **WCF (CoreWCF) over three transports** against **gRPC** for two
unary methods, on .NET 10 / C#: a single-order `GetOrderById` and a batch `GetOrders`
(10,000 orders by default). The published results below are all `GetOrderById`.

This benchmark was done as a fun excercise. Be aware of the following caveats
- Both client and server were ran on the same machine.
- The response of the `GetOrderById` method is hardcoded.  There is no additional logic apart from just returning an `Order` instance
- No finetuning was done on the gRPC or WCF servers
- The duration of the benchmark is just 60 sec (with a 10 sec warmup time)
- For `GetOrders` the client allocates roughly 1.5 KB of managed heap per order, so those runs are
  allocation-bound long before they are transport-bound. Read the **Memory / GC** block of the
  report before comparing them. See [Benchmarking `GetOrders`](#benchmarking-getorders).
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

Throughput at each concurrency level

![Throughput vs Concurrency](./docs/Chart1.png)


| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | **21,698** | 16,106 | 14,338 | 12,286 |
| 10 | **81,292** | 76,832 | 60,995 | 52,570 |
| 20 | **82,315** | 78,717 | 58,438 | 51,589 |
| 40 | **81,015** | 78,997 | 57,621 | 51,702 |
| 60 | **79,381** | 76,919 | 56,024 | 50,200 |
| 80 | **78,472** | 76,032 | 54,638 | 48,392 |
| 100 | **76,710** | 74,960 | 54,193 | 45,654 |


### Raw data

 **Zero failed calls across all 28 runs** (~98.9M successful requests). Latency in milliseconds.

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp` | 1 | 21,698 req/s | 0.039 | 0.059 | 0.070 | 0.111 | 0.046 | 203.172 |
| `nettcp` | 10 | 81,292 req/s | 0.103 | 0.129 | 0.144 | 0.727 | 0.123 | 505.001 |
| `nettcp` | 20 | 82,315 req/s | 0.202 | 0.261 | 0.664 | 1.118 | 0.243 | 7.784 |
| `nettcp` | 40 | 81,015 req/s | 0.399 | 1.010 | 1.271 | 1.712 | 0.494 | 7.862 |
| `nettcp` | 60 | 79,381 req/s | 0.608 | 1.506 | 1.733 | 2.293 | 0.756 | 12.979 |
| `nettcp` | 80 | 78,472 req/s | 0.827 | 1.902 | 2.148 | 2.937 | 1.019 | 9.997 |
| `nettcp` | 100 | 76,710 req/s | 1.066 | 2.329 | 2.621 | 3.622 | 1.303 | 13.3 |
| `grpc` | 1 | 16,106 req/s | 0.056 | 0.085 | 0.109 | 0.177 | 0.062 | 4.444 |
| `grpc` | 10 | 76,832 req/s | 0.118 | 0.148 | 0.161 | 0.676 | 0.130 | 9.177 |
| `grpc` | 20 | 78,717 req/s | 0.230 | 0.289 | 0.316 | 1.085 | 0.254 | 12.89 |
| `grpc` | 40 | 78,997 req/s | 0.452 | 0.611 | 1.118 | 1.691 | 0.506 | 4.764 |
| `grpc` | 60 | 76,919 req/s | 0.685 | 1.244 | 1.784 | 2.315 | 0.780 | 10.504 |
| `grpc` | 80 | 76,032 req/s | 0.925 | 1.812 | 2.285 | 2.868 | 1.052 | 12.033 |
| `grpc` | 100 | 74,960 req/s | 1.172 | 2.334 | 2.795 | 3.547 | 1.334 | 11.272 |
| `basichttp` | 1 | 14,338 req/s | 0.059 | 0.101 | 0.126 | 0.206 | 0.070 | 16.77 |
| `basichttp` | 10 | 60,995 req/s | 0.129 | 0.169 | 0.200 | 1.057 | 0.164 | 9.836 |
| `basichttp` | 20 | 58,438 req/s | 0.249 | 0.355 | 1.159 | 1.721 | 0.342 | 602.473 |
| `basichttp` | 40 | 57,621 req/s | 0.493 | 1.663 | 1.933 | 3.041 | 0.694 | 17.048 |
| `basichttp` | 60 | 56,024 req/s | 0.754 | 2.318 | 2.658 | 4.176 | 1.071 | 23.627 |
| `basichttp` | 80 | 54,638 req/s | 0.991 | 3.010 | 3.500 | 5.189 | 1.464 | 23.892 |
| `basichttp` | 100 | 54,193 req/s | 1.261 | 3.618 | 4.318 | 6.047 | 1.845 | 32.385 |
| `wshttp` | 1 | 12,286 req/s | 0.066 | 0.121 | 0.131 | 0.245 | 0.081 | 231.218 |
| `wshttp` | 10 | 52,570 req/s | 0.149 | 0.199 | 0.253 | 1.138 | 0.190 | 8.61 |
| `wshttp` | 20 | 51,589 req/s | 0.284 | 0.443 | 1.290 | 1.730 | 0.388 | 12.453 |
| `wshttp` | 40 | 51,702 req/s | 0.540 | 1.795 | 2.053 | 3.254 | 0.773 | 14.087 |
| `wshttp` | 60 | 50,200 req/s | 0.820 | 2.519 | 2.927 | 4.435 | 1.195 | 21.527 |
| `wshttp` | 80 | 48,392 req/s | 1.156 | 3.250 | 3.854 | 5.452 | 1.653 | 26.224 |
| `wshttp` | 100 | 45,654 req/s | 1.554 | 4.106 | 4.969 | 6.825 | 2.190 | 33.793 |


### Findings (take this with huge grain of salt)

1. **Perf ranking: `nettcp` > `grpc` > `basichttp` > `wshttp`** at every concurrency level. `nettcp` wins 
over `basichttp` and `wshttp` as expected. Sending binary data over raw TCP is of course faster than
SOAP/XML over http.  `nettcp` also wins over `grpc` but barely.  Both uses binary (protobuf for grpc)
data.  Could it be that HTTP2 added a significant overhead as compared to raw TCP?

2. **Throughput of grpc and wcf services increased signicantly until concurrency level somewhere between 10 and 20.**. Throughput rises sharply then plateus

3. **Latency grows roughly linearly with concurrency while throughput flattens** 

4. **`wshttp` is consistently last** on both throughput and latency. The SOAP 1.2 / WS-Addressing envelope plus TLS costs adds the most overhead.

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

