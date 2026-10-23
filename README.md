# WCF vs gRPC Benchmark

Micro-benchmark comparing **WCF (CoreWCF) over three transports** against **gRPC** for two
unary methods, on .NET 10 / C#: a single-order `GetOrderById` and a batch `GetOrders`. Both
operations are run across the full concurrency range

This benchmark was done as a fun exercise. Please be aware of the following caveats
- Both client and server were ran on the same machine.
- The response of the `GetOrderById` method is hardcoded.  There is no additional logic apart from just returning an `Order` instance
- No fine tuning was done on the gRPC or WCF servers
- The duration of the benchmark is just 60 sec `-d 60` (with a 10 sec warmup time `-w 10`)
- **Both processes run Server GC**, set explicitly in each csproj.
- The WCF net.tcp endpoint needs `MaxBufferSize` raised well above the 64 KB default or the batch
  operation fails outright. See [Notes on the harness](#notes-on-the-harness).

> Since this test was done on a single machine, and since WCF and gRPC are both built on top ASP.NET Core,
> the performance differences below are likely due protobuf (grpc), binary xml (nettcp), and SOAP xml serialization.

## Transports compared

| Target | Transport | Endpoint |
|---|---|---|
| `wcf` `basichttp` | CoreWCF `BasicHttpBinding` (SOAP 1.1) | `http://localhost:5000/NorthwindWcfService/basic` |
| `wcf` `wshttp` | CoreWCF `WSHttpBinding` (SOAP 1.2 over HTTPS) | `https://localhost:5001/NorthwindWcfService/ws` |
| `wcf` `nettcp` | CoreWCF `NetTcpBinding` (binary over raw TCP) | `net.tcp://localhost:5002/NorthwindWcfService/nettcp` |
| `grpc` | gRPC over HTTP/2 | `http://localhost:5003` |

## Service methods

Both grpc and WCF services expose the same two operations, so the transports can be compared on a single-order
and a batch response.

| Method | gRPC | WCF | Returns |
|---|---|---|---|
| `GetOrderById` | `rpc GetOrderById(OrderRequest) returns (Order)` | `Order GetOrderById(OrderRequest)` | one hardcoded order |
| `GetOrders` | `rpc GetOrders(OrdersRequest) returns (OrdersResponse)` | `OrdersResponse GetOrders(OrdersRequest)` | **10,000** orders |


```
WCF     OrdersResponse { Order[] Orders }
gRPC    OrdersResponse { repeated Order orders }
```

`GetOrders` takes an optional `Count` (gRPC) / `Count` (WCF `OrdersRequest`) which determines the number of orders returned.
The batch is materialized once and cached per process in order to avoid measuring the object allocation/de-allocation
 
Measured wire response payload on .NET 10

| Transport | Encoding | **n = 1,000** | n = 10,000 | bytes/order | vs gRPC |
|---|---|---|---|---|---|
| `grpc` | protobuf | **263,626 B** (0.251 MB) | 2,638,546 B | 263.9 | 1.00x |
| `nettcp` | binary XML | **323,785 B** (0.309 MB) | 3,234,685 B | 323.4 | 1.23x |
| `basichttp` | SOAP 1.1 XML | **1,154,714 B** (1.101 MB) | 11,565,085 B | 1156.7 | 4.38x |
| `wshttp` | SOAP 1.2 + WS-Addressing | **1,154,948 B** (1.101 MB) | 11,565,319 B | 1156.7 | 4.38x |

SOAP is **4.4x more verbose than protobuf** and 3.6x more verbose than net.tcp's binary XML. Measured
four different ways: `curl` reports the exact HTTP body length for the two SOAP transports,
`CalculateSize()` gives the exact protobuf encoding for gRPC, and a byte-counting TCP proxy measures
the real net.tcp framing

## Environment

- macOS 26.5.2, Apple M4, 10 cores, 32 GB
- .NET SDK 10.0.100, `net10.0`
- CoreWCF 1.9.1, gRPC 2.84.0, `System.ServiceModel.Http` 10.0.652802

## Test Setup
- server warmup time `10 sec` (done to exclude connection setup and JIT time)
- test duration `60 sec` 
- concurrency levels `1/10/20/40/60/80/100` (simulates the number of concurrent clients. Each client has its own channel)

# Single Order Response Payload Benchmark

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
   also wins over `grpc` but not by much.
2. **Throughput rises steeply to `-c 10` and then plateaus.** 
3. **Latency grows roughly linearly with concurrency while throughput flattens.**
4. **`wshttp` is consistently last** on both throughput and latency at every level**

### Concurrency (important)

`-c N` creates **N independent clients, for gRPC as well as WCF**. Each `GrpcPerf` owns its own
`GrpcChannel`, so N workers means N HTTP/2 connections.

# Batch Order Response Payload Benchmark

`-op getorders` switches the harness to the batch operation. The same `-w`, `-d` and `-c` knobs
apply

```bash
Bench.Client -target grpc -op getorders -n 1000 -w 10 -d 60 -c 8
Bench.Client -target wcf  -arg nettcp -op getorders -n 1000 -w 10 -d 60 -c 8
```
 
## `GetOrders` results

1,000 orders per request, with 10 sec warmup and duration of 60 sec, `-w 10 -d 60`. Chart below shows throughput (req/sec) and p99 tail latency at each concurrency level

![GetOrders throughput and p99 latency across 7 concurrency levels](./docs/getorders.png)

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 444 req/s | **934 req/s** | 228 req/s  | 204 req/s  |
| 10 | 1,736 req/s | **4,694 req/s** | 922 req/s  | 746 req/s  |
| 20 | 1,817 req/s | **4,682 req/s** | 994 req/s  | 770 req/s  |
| 40 | 1,880 req/s | **4,689 req/s** | 1,002 req/s  | 803 req/s  |
| 60 | 1,878 req/s  | **4,473 req/s** | 1,005 req/s  | 802 req/s  |
| 80 | 1,917 req/s  | **4,388 req/s** | 1,025 req/s  | 814 req/s  |
| 100 | 1,934 req/s  | **4,350 req/s** | 1,020 req/s  | 816 req/s  |

### Raw data

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp` | 1 | 444 req/s | 2.128 | 2.375 | 2.465 | 5.144 | 2.254 | 27.479 |
| `grpc` | 1 | 934 req/s | 1.017 | 1.181 | 1.312 | 2.009 | 1.070 | 9.719 |
| `basichttp` | 1 | 228 req/s | 4.137 | 5.273 | 5.859 | 7.403 | 4.391 | 25.549 |
| `wshttp` | 1 | 204 req/s | 4.559 | 5.947 | 6.733 | 8.936 | 4.896 | 29.355 |
| `nettcp` | 10 | 1,736 req/s | 4.855 | 9.339 | 11.255 | 16.266 | 5.760 | 50.305 |
| `grpc` | 10 | 4,694 req/s | 1.940 | 2.643 | 3.367 | 6.330 | 2.130 | 39.134 |
| `basichttp` | 10 | 922 req/s | 9.761 | 16.122 | 18.629 | 26.675 | 10.847 | 134.948 |
| `wshttp` | 10 | 746 req/s | 12.307 | 19.464 | 22.516 | 31.390 | 13.397 | 82.683 |
| `nettcp` | 20 | 1,817 req/s | 9.366 | 18.698 | 23.235 | 37.112 | 11.005 | 129.204 |
| `grpc` | 20 | 4,682 req/s | 3.858 | 5.955 | 7.171 | 10.988 | 4.271 | 52.088 |
| `basichttp` | 20 | 994 req/s | 17.279 | 33.606 | 41.731 | 64.480 | 20.113 | 169.685 |
| `wshttp` | 20 | 770 req/s | 23.085 | 41.090 | 49.242 | 71.305 | 25.979 | 195.033 |
| `nettcp` | 40 | 1,880 req/s | 18.606 | 35.169 | 43.014 | 65.812 | 21.270 | 175.177 |
| `grpc` | 40 | 4,689 req/s | 7.411 | 12.930 | 15.022 | 24.561 | 8.530 | 65.420 |
| `basichttp` | 40 | 1,002 req/s | 34.350 | 68.228 | 84.825 | 124.953 | 39.910 | 254.826 |
| `wshttp` | 40 | 803 req/s | 45.867 | 76.392 | 90.085 | 128.511 | 49.803 | 261.962 |
| `nettcp` | 60 | 1,878 req/s | 28.303 | 51.852 | 63.255 | 95.853 | 31.937 | 212.789 |
| `grpc` | 60 | 4,473 req/s | 11.433 | 20.790 | 25.253 | 39.326 | 13.410 | 92.394 |
| `basichttp` | 60 | 1,005 req/s | 53.533 | 96.330 | 117.197 | 164.164 | 59.677 | 388.206 |
| `wshttp` | 60 | 802 req/s | 71.736 | 105.511 | 119.891 | 156.380 | 74.829 | 326.092 |
| `nettcp` | 80 | 1,917 req/s | 37.585 | 66.350 | 80.570 | 117.231 | 41.714 | 284.457 |
| `grpc` | 80 | 4,388 req/s | 15.559 | 28.600 | 36.895 | 50.911 | 18.231 | 137.239 |
| `basichttp` | 80 | 1,025 req/s | 71.806 | 123.902 | 146.518 | 199.756 | 78.047 | 396.334 |
| `wshttp` | 80 | 814 req/s | 94.668 | 137.453 | 156.465 | 203.595 | 98.269 | 367.045 |
| `nettcp` | 100 | 1,934 req/s | 46.910 | 81.489 | 96.463 | 137.275 | 51.682 | 406.624 |
| `grpc` | 100 | 4,350 req/s | 19.687 | 36.110 | 44.743 | 60.556 | 22.985 | 133.113 |
| `basichttp` | 100 | 1,020 req/s | 92.204 | 143.550 | 167.016 | 225.418 | 97.969 | 412.973 |
| `wshttp` | 100 | 816 req/s | 117.893 | 174.123 | 197.075 | 254.869 | 122.566 | 424.045 |

### Batch findings

1. **Payload size accounts for most of the differences in performance.**
   Measured response bytes are in [Service methods](#service-methods): at 1,000 orders protobuf
   is at 264 KB, net.tcp binary XML 324 KB, and either SOAP transport 1.15 MB.  
 
2. **gRPC is the only transport that loses throughput as concurrency rises.** It peaks at concurrency `-c 10`
   with 4,694 req/s and decays 7% to 4,350 by `-c 100`, while the three WCF transports hold a flat
   plateau over the same range,

3. **gRPC has the lowest (lower is better) tail latency at every level.** At `-c 100` its p99 is 60.6 ms against
   `nettcp`'s 137.3, `basichttp`'s 225.4 and `wshttp`'s 254.9.


### Batch-size sweep

`GetOrders` was re-run at four more batch sizes - 100, 300, 500, 700 orders per call - each the same 28-run sweep as the 1,000-order results above. Across the five batch sizes that is **140 runs, 53,067,893 calls and zero failures**.

![Batch-size sweep: the gRPC / nettcp throughput ratio against batch size](./docs/batch-sweep.png)

gRPC throughput divided by `nettcp` throughput, at every batch size and concurrency level:

| Batch size | c=1 | c=10 | c=20 | c=40 | c=60 | c=80 | c=100 |
|---|---|---|---|---|---|---|---|
| 100 | 1.65x | 1.77x | 1.79x | 1.78x | 1.77x | 1.82x | 1.80x |
| 300 | 1.73x | 2.11x | 2.23x | 2.24x | 2.17x | 2.29x | 2.04x |
| 500 | 1.78x | 2.36x | 2.38x | 2.28x | 2.16x | 2.08x | 2.05x |
| 700 | 1.95x | 2.34x | 2.40x | 2.21x | 2.20x | 2.16x | 2.14x |
| 1,000 | 2.10x | 2.70x | 2.58x | 2.49x | 2.38x | 2.29x | 2.25x |

### Batch-size findings

1. **The ranking doesnt change.** `gRPC` is first, `nettcp` second, `basichttp` third and `wshttp` last. The larger the batch size,
   the more gRPC wins by, while `nettcp` stays in second place.

2. **gRPC's margin over `nettcp` grows with the batch size.**
 
3. **gRPC has the best p99 at every batch size and every concurrency level**

### Batch-size raw data

Throughput in req/s; each call returns n orders, so multiply by n for orders/s.

**100 orders per call**

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 3,825 | **6,327** | 2,009 | 1,808 |
| 10 | 19,310 | **34,262** | 8,154 | 6,736 |
| 20 | 21,607 | **38,759** | 9,399 | 7,838 |
| 40 | 21,223 | **37,766** | 9,327 | 8,046 |
| 60 | 20,993 | **37,205** | 9,332 | 8,103 |
| 80 | 21,599 | **39,246** | 9,742 | 8,116 |
| 100 | 21,496 | **38,593** | 9,652 | 7,327 |

**300 orders per call**

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 1,428 | **2,471** | 753 | 703 |
| 10 | 6,780 | **14,331** | 2,981 | 2,470 |
| 20 | 6,834 | **15,252** | 3,162 | 2,598 |
| 40 | 7,035 | **15,749** | 3,300 | 2,674 |
| 60 | 7,119 | **15,446** | 3,346 | 2,675 |
| 80 | 6,686 | **15,295** | 3,161 | 2,617 |
| 100 | 7,076 | **14,468** | 3,416 | 2,721 |

**500 orders per call**

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 965 | **1,722** | 471 | 429 |
| 10 | 4,045 | **9,528** | 1,936 | 1,583 |
| 20 | 4,365 | **10,368** | 2,061 | 1,649 |
| 40 | 4,356 | **9,933** | 2,090 | 1,677 |
| 60 | 4,473 | **9,659** | 2,125 | 1,705 |
| 80 | 4,419 | **9,203** | 2,125 | 1,719 |
| 100 | 4,445 | **9,095** | 2,122 | 1,721 |

**700 orders per call**

| Concurrency | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1 | 661 | **1,286** | 343 | 317 |
| 10 | 3,073 | **7,176** | 1,471 | 1,210 |
| 20 | 3,272 | **7,852** | 1,582 | 1,266 |
| 40 | 3,308 | **7,317** | 1,584 | 1,284 |
| 60 | 3,292 | **7,229** | 1,599 | 1,292 |
| 80 | 3,308 | **7,160** | 1,609 | 1,294 |
| 100 | 3,314 | **7,101** | 1,584 | 1,293 |

# Running

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
 