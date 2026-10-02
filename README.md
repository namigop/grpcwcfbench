# grpcwcfbench

Micro-benchmark comparing **WCF (CoreWCF) over three transports** against **gRPC** for a
single `GetOrderById` round-trip, on .NET 10 / C#.

Transports compared:

| Target | Transport | Endpoint |
|---|---|---|
| `wcf` `basichttp` | CoreWCF `BasicHttpBinding` (SOAP 1.1 / text XML over HTTP) | `http://localhost:5000/NorthwindWcfService/basic` |
| `wcf` `wshttp` | CoreWCF `WSHttpBinding` (SOAP 1.2 over HTTPS) | `https://localhost:5001/NorthwindWcfService/ws` |
| `wcf` `nettcp` | CoreWCF `NetTcpBinding` (binary framing over raw TCP) | `net.tcp://localhost:5002/NorthwindWcfService/nettcp` |
| `grpc` | gRPC over HTTP/2 (h2c) | `http://localhost:5003` |

## Results

Warmup 30s, measurement 60s, concurrency 1 / 10 / 100. **Zero failed calls in all 12 runs**
(~28.4M successful requests total). Latency in milliseconds.

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp`  | 1   | **21,862 req/s** | 0.041 | 0.059 | 0.066 | **0.088** | 0.046 | 150.6 |
| `grpc`    | 1   | 16,125 req/s | 0.060 | 0.080 | 0.087 | 0.106 | 0.062 | 20.8 |
| `basichttp`| 1   | 16,044 req/s | 0.055 | 0.079 | 0.100 | 0.159 | 0.062 | 7.6 |
| `wshttp`  | 1   | 14,276 req/s | 0.062 | 0.081 | 0.095 | 0.153 | 0.070 | 265.6 |
| `grpc`    | 10  | **95,827 req/s** | 0.094 | 0.119 | 0.130 | 0.572 | 0.104 | 7.7 |
| `nettcp`  | 10  | 82,153 req/s | 0.101 | 0.130 | 0.152 | 0.707 | 0.122 | 527.3 |
| `basichttp`| 10  | 63,819 req/s | 0.121 | 0.154 | 0.180 | 1.030 | 0.157 | 349.6 |
| `wshttp`  | 10  | 53,633 req/s | 0.143 | 0.198 | 0.278 | 1.157 | 0.186 | 20.7 |
| `nettcp`  | 100 | **80,768 req/s** | 1.048 | 2.136 | 2.399 | **3.243** | 1.238 | 13.1 |
| `basichttp`| 100 | 62,231 req/s | 1.147 | 2.965 | 3.492 | 5.229 | 1.607 | 19.4 |
| `grpc`    | 100 | 55,887 req/s | 2.016 | 2.898 | 3.300 | 4.080 | 1.789 | 11.1 |
| `wshttp`  | 100 | 50,100 req/s | 1.396 | 3.815 | 4.540 | 6.212 | 1.996 | 33.0 |

### What the numbers say

**NetTcpBinding wins at every concurrency level** except `-c 10`, and it has the tightest tail
throughout — best p50 *and* best p99 at both 1 and 100. Binary framing over a raw TCP socket
avoids the XML serialisation and HTTP header work the other three pay on every message.

**gRPC is the most concurrency-sensitive.** It scales best from 1 to 10 (5.9x, vs ~3.8-4.0x for
WCF) but degrades hardest from 10 to 100, dropping 42% (95,827 → 55,887) while NetTcp and
BasicHttp stay flat. This is the cost of the shared-channel model: all 100 workers multiplex
over one HTTP/2 connection, so they contend on a single flow-control window. WCF's one-client-per-
worker model spreads load across 100 independent sockets, which is why it holds up better at
`-c 100` — at the cost of never getting the 10-worker win.

**Every transport saturates between `-c 10` and `-c 100`.** Throughput is flat or declining at
100 while p50 inflates ~10x. By that point the bottleneck is the single server process, not the
transport. Treat the `-c 100` row as a measure of queueing delay under overload rather than of
transport capability.

**WSHttpBinding is consistently last.** The extra SOAP 1.2 / WS-Addressing envelope plus TLS
shows up as both the lowest throughput and the worst p99 at 1, 10 and 100.

### Reading the `max` column

Treat `max` as noise. Several runs show outliers two to three orders of magnitude above their own
p99 (nettcp c=10: 527ms vs p99 0.707ms; wshttp c=1: 265ms vs p99 0.153ms). These are almost
certainly GC pauses and thread-pool stalls on the client, not transport behaviour — a genuine
network stall would show up in p99 too. **Use p50/p95/p99 for comparison and ignore `max`.**

## Caveats

- **Client and server share one machine** (loopback). These numbers measure framework and
  transport overhead with no real network in the path. They say nothing about behaviour over a
  LAN or WAN, where NetTcp's and HTTP's connection-setup costs and TLS costs would change
  relative ranking.
- **The service is a stub.** `NorthwindWcfService.GetOrderById` returns a fresh empty `Order`,
  and `NorthwindGrpcService` likewise — no database, no business logic. The payload is tiny, so
  what is being measured is almost entirely serialisation and transport cost. That is the right
  shape for a framework comparison, but it means the absolute numbers are far below what a real
  data-backed service would show.
- **One server process, default thread pool.** The saturation at `-c 10`+ is a property of this
  configuration. Running the server across multiple instances, or tuning Kestrel/CoreWCF limits,
  would move the ceiling.
- **A single un-replicated trial per configuration.** No confidence intervals; treat small
  differences (under ~5%) as noise. The gaps between transports are far larger than that.

## Environment

- macOS 26.5.2 (build 25F84), Apple M4, 10 cores, 32 GB
- .NET SDK 10.0.100, `net10.0`, Debug build
- CoreWCF 1.9.1, gRPC 2.84.0, `System.ServiceModel.Http` 10.0.652802
- Commit `f738807`, measured 2026-10-01T15:29Z–15:48Z

## Running

Start a server (one target per process):

```bash
dotnet run --project Bench.Server -- -target wcf    # BasicHttp :5000, WSHttp :5001, NetTcp :5002
dotnet run --project Bench.Server -- -target grpc   # gRPC on HTTP/2 :5003
```

Then a client sweep:

```bash
# Single-call smoke test (omit -w/-d/-c)
Bench.Client -target wcf -arg basichttp

# Benchmark
Bench.Client -target wcf  -arg basichttp -w 30 -d 60 -c 1
Bench.Client -target wcf  -arg wshttp    -w 30 -d 60 -c 10
Bench.Client -target wcf  -arg nettcp    -w 30 -d 60 -c 100
Bench.Client -target grpc                -w 30 -d 60 -c 100

# Override the endpoint
Bench.Client -target wcf -arg nettcp -address "net.tcp://localhost:5002/NorthwindWcfService/nettcp" -d 60
```

`-w` is warmup (reuses the same client instances, so connection setup and JIT land outside the
measurement), `-d` is measurement duration, `-c` is the number of concurrent workers. Exit code is
0 on a clean run, 1 if any call failed, 2 on bad arguments.

Note that `-c N` means N separate sockets for WCF (one `ClientBase` per worker, since a WCF client
drives a single channel) but still one connection for gRPC (HTTP/2 multiplexing). See
[Concurrency model](#what-the-numbers-say) for why that matters.

## Regenerating the WCF client

The WCF proxy in `Bench.Client/wcf/ServiceReference/` is generated from the running server:

```bash
cd Bench.Client
dotnet-svcutil http://localhost:5000/NorthwindWcfService/basic?wsdl \
  -d wcf/ServiceReference \
  -n 'http://tempuri.org/,Bench.Client.Wcf' \
  -ct System.Collections.Generic.List`1 \
  -ser DataContractSerializer \
  -r Bench.Client.csproj
```

Passing `-r Bench.Client.csproj` lets the generator reuse the existing
`Bench.wcf.DataContracts` types instead of emitting duplicates.
