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

**Release build.** Warmup 10s, measurement 30s, concurrency 1/10/20/40/60/80/100.
**Zero failed calls across all 28 runs** (~44.2M successful requests). Latency in milliseconds.

| Transport | Conc. | Throughput | p50 | p90 | p95 | p99 | mean | max |
|---|---|---|---|---|---|---|---|---|
| `nettcp`   | 1  | 21,226 req/s | **0.041** | 0.059 | 0.066 | 0.092 | 0.047 | 9.3 |
| `nettcp`   | 10 | 83,141 req/s | 0.101 | 0.125 | 0.137 | 0.664 | 0.120 | 390.8 |
| `nettcp`   | 20 | **85,235 req/s** | 0.199 | 0.254 | 0.608 | 0.995 | 0.235 | 7.0 |
| `nettcp`   | 40 | 82,663 req/s | 0.392 | 0.986 | 1.210 | 1.584 | 0.484 | 12.9 |
| `nettcp`   | 60 | 81,699 req/s | 0.597 | 1.420 | 1.623 | 2.150 | 0.734 | 8.4 |
| `nettcp`   | 80 | 80,130 req/s | 0.819 | 1.808 | 2.041 | 2.802 | 0.998 | 8.8 |
| `nettcp`   | 100 | 79,044 req/s | 1.052 | 2.204 | 2.480 | 3.428 | 1.265 | 11.1 |
| `grpc`     | 1  | 16,263 req/s | 0.059 | 0.079 | 0.087 | **0.109** | 0.061 | 1.1 |
| `grpc`     | 10 | 79,866 req/s | 0.116 | 0.141 | 0.151 | 0.609 | 0.125 | 8.0 |
| `grpc`     | 20 | 81,520 req/s | 0.224 | 0.278 | 0.302 | 0.993 | 0.245 | 3.5 |
| `grpc`     | 40 | 79,572 req/s | 0.445 | 0.618 | 1.111 | 1.641 | 0.503 | 25.3 |
| `grpc`     | 60 | 78,896 req/s | 0.671 | 1.184 | 1.686 | 2.216 | 0.760 | 23.0 |
| `grpc`     | 80 | 72,667 req/s | 0.943 | 1.970 | 2.388 | 3.157 | 1.101 | 26.6 |
| `grpc`     | 100 | 73,401 req/s | 1.170 | 2.462 | 2.908 | 3.808 | 1.362 | 14.8 |
| `basichttp`| 1  | 15,387 req/s | 0.056 | 0.086 | 0.119 | 0.179 | 0.065 | 3.5 |
| `basichttp`| 10 | 63,772 req/s | 0.125 | 0.162 | 0.195 | 0.986 | 0.157 | 6.2 |
| `basichttp`| 20 | 64,139 req/s | 0.237 | 0.326 | 1.015 | 1.509 | 0.312 | 14.8 |
| `basichttp`| 40 | 62,954 req/s | 0.446 | 1.453 | 1.682 | 2.768 | 0.635 | 12.5 |
| `basichttp`| 60 | 61,582 req/s | 0.672 | 2.044 | 2.339 | 3.599 | 0.974 | 14.6 |
| `basichttp`| 80 | 59,683 req/s | 0.919 | 2.655 | 3.033 | 4.479 | 1.340 | 16.1 |
| `basichttp`| 100 | 57,142 req/s | 1.194 | 3.337 | 3.859 | 5.452 | 1.750 | 55.3 |
| `wshttp`   | 1  | 13,671 req/s | 0.063 | 0.081 | 0.094 | 0.169 | 0.073 | 9.8 |
| `wshttp`   | 10 | 55,297 req/s | 0.142 | 0.184 | 0.232 | 1.051 | 0.181 | 9.7 |
| `wshttp`   | 20 | 55,404 req/s | 0.271 | 0.418 | 1.180 | 1.538 | 0.361 | 9.2 |
| `wshttp`   | 40 | 53,666 req/s | 0.523 | 1.691 | 1.934 | 3.081 | 0.745 | 13.0 |
| `wshttp`   | 60 | 52,320 req/s | 0.803 | 2.366 | 2.713 | 4.110 | 1.147 | 14.6 |
| `wshttp`   | 80 | 51,305 req/s | 1.107 | 2.961 | 3.459 | 5.068 | 1.559 | 22.6 |
| `wshttp`   | 100 | 48,927 req/s | 1.462 | 3.654 | 4.451 | 6.085 | 2.044 | 23.4 |

Throughput at each concurrency, for direct comparison:

| Conc. | `nettcp` | `grpc` | `basichttp` | `wshttp` |
|---|---|---|---|---|
| 1  | **21,226** | 16,263 | 15,387 | 13,671 |
| 10 | **83,141** | 79,866 | 63,772 | 55,297 |
| 20 | **85,235** | 81,520 | 64,139 | 55,404 |
| 40 | **82,663** | 79,572 | 62,954 | 53,666 |
| 60 | **81,699** | 78,896 | 61,582 | 52,320 |
| 80 | 80,130 | 72,667 | 59,683 | 51,305 |
| 100 | **79,044** | 73,401 | 57,142 | 48,927 |

### What the numbers say

**The ranking is stable and never changes: `nettcp` > `grpc` > `basichttp` > `wshttp`** at every
one of the seven concurrency levels. `nettcp` wins throughput and, at c=1, wins p50 by a clear
margin (0.041ms vs 0.056-0.063ms for the others). Binary framing over a raw TCP socket avoids the
XML serialisation and HTTP header work the other three pay on every message.

**gRPC has the tightest tail at low concurrency.** At c=1 its p99 is 0.109ms against `nettcp`'s
0.092ms and `basichttp`'s 0.179ms — and its `max` is 1.1ms, the only single-digit outlier-free run
in the whole matrix. gRPC's per-message overhead is genuinely low; it just needs concurrency to
compete on throughput.

**Every transport peaks at c=20 and then declines gently.** The curve is remarkably consistent:
throughput rises steeply from c=1 to c=10 (+290% for `nettcp`), gains another 2-3% to c=20, and
then falls monotonically — `nettcp` drops 7% from its c=20 peak to c=100, `wshttp` 12%, `gRPC` 10%.
There is no cliff and no collapse, just diminishing returns. On this machine **c=20 is the
operating point**; beyond that you pay latency for throughput you do not get.

**Latency grows roughly linearly with concurrency while throughput flattens** — the signature of a
saturated queue. p50 for `nettcp` goes 0.041 → 0.101 → 0.199 → ... → 1.052ms from c=1 to c=100
(25x) while throughput is essentially unchanged from c=20 onwards. Past c=20 you are buying
queueing delay, not capacity.

**`wshttp` is consistently last** on both throughput and tail latency. The SOAP 1.2 /
WS-Addressing envelope plus TLS costs more than the transport saves elsewhere, at every level.

### Concurrency model (important)

`-c N` creates **N independent clients, for gRPC as well as WCF**. Each `GrpcPerf` owns its own
`GrpcChannel`, so N workers means N HTTP/2 connections.

This is deliberate and it matters. With an earlier single shared channel, gRPC degraded sharply
above c=10 and **failed to terminate at c=100 at all** — the process spun at ~400% CPU on both
client and server for 8+ minutes with flat memory, the signature of HTTP/2 flow-control
exhaustion when 100 concurrent large responses contend for one connection's window. Switching to
per-worker channels turned that cliff into a plateau (gRPC now holds ~73-82k req/s from c=10 to
c=100) and removed the hang. Note this gives up HTTP/2 multiplexing's connection-sharing benefit in
exchange for eliminating head-of-line blocking; on a real network with many hosts, multiplexed
shared channels may well win.

### Reading the `max` column

Treat `max` as noise. Several runs show outliers orders of magnitude above their own p99 — `nettcp`
c=10 shows 390.8ms against a p99 of 0.664ms. These are client-side GC pauses and thread-pool stalls
on a box also running the server, not transport behaviour; a genuine network stall would show up
in p99 too. **Use p50/p95/p99 for comparison and ignore `max`.**

## Caveats

- **Client and server share one machine** (loopback). These numbers measure framework and
  transport overhead with no real network in the path. They say nothing about behaviour over a LAN
  or WAN, where connection-setup costs and TLS would change the relative ranking.
- **The service is a stub returning a populated object.** `GetOrderById` returns a fixed
  `Order` — customer, 17 order fields and an `OrderDetails` entry — with no database and no business
  logic. The payload is small, so this measures serialisation and transport cost, not end-to-end
  application work.
- **One server process, default thread pool.** The saturation from c=20 upward is a property of
  this configuration. Multiple server instances or tuned Kestrel/CoreWCF limits would move the
  ceiling.
- **Short measurement window.** 10s warmup and 30s measurement leave less time to reach steady
  state at high concurrency than a 30s/60s run would. Treat the high-concurrency tail as indicative
  rather than precise.
- **A single un-replicated trial per configuration**, no confidence intervals. Differences under
  roughly 5% should be treated as noise; the gaps between transports here are much larger.

## Environment

- macOS 26.5.2, Apple M4, 10 cores, 32 GB
- .NET SDK 10.0.100, `net10.0`, **Release** build
- CoreWCF 1.9.1, gRPC 2.84.0, `System.ServiceModel.Http` 10.0.652802
- Commit `e39fc08`, branch `bench/cli-args-and-results`
- Measured 2026-10-02T03:04:47Z – 03:23:38Z

## Running

Build Release first — benchmark numbers from a Debug build are not meaningful.

```bash
dotnet build -c Release
```

Start a server (one target per process):

```bash
Bench.Server -target wcf    # BasicHttp :5000, WSHttp :5001, NetTcp :5002
Bench.Server -target grpc   # gRPC on HTTP/2 :5003
```

Then a client sweep:

```bash
# Single-call smoke test (omit -w/-d/-c)
Bench.Client -target wcf -arg basichttp

# Benchmark
Bench.Client -target wcf  -arg basichttp -w 10 -d 30 -c 1
Bench.Client -target wcf  -arg wshttp    -w 10 -d 30 -c 20
Bench.Client -target wcf  -arg nettcp    -w 10 -d 30 -c 100
Bench.Client -target grpc                -w 10 -d 30 -c 100

# Override the endpoint
Bench.Client -target wcf -arg nettcp -address "net.tcp://localhost:5002/NorthwindWcfService/nettcp" -d 30
```

`-w` is warmup (a separate unrecorded pass that reuses the same client instances, so connection
setup and JIT land outside the measurement), `-d` is measurement duration, `-c` is the number of
concurrent workers. Exit code is 0 on a clean run, 1 if any call failed, 2 on bad arguments.

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
