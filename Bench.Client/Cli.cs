using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Bench;
using Bench.Client;

/// <summary>Parsed command line: the target selection plus the benchmark knobs.</summary>
public sealed class Cli {
    const string BasicHttpAddress = "http://localhost:5000/NorthwindWcfService/basic";
    const string WsHttpAddress = "https://localhost:5001/NorthwindWcfService/ws";
    const string NetTcpAddress = "net.tcp://localhost:5002/NorthwindWcfService/nettcp";
    const string GrpcAddress = "http://localhost:5003";

    public WcfTransport Transport { get; private set; }
    public bool UseGrpc { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public int Concurrency { get; private set; } = 1;
    public int? Warmup { get; private set; }
    public int? Duration { get; private set; }
    public BenchOperation Operation { get; private set; } = BenchOperation.GetOrderById;
    public int Count { get; private set; }

    public string Describe() {
        var operation = Operation == BenchOperation.GetOrders
            ? $"getorders(n={OrderData.ResolveCount(Count):N0})"
            : "getorderbyid";
        return UseGrpc
            ? $"gRPC over HTTP/2 — {Address} — {operation}"
            : $"WCF over {Transport}Binding — {Address} — {operation}";
    }

    public static bool TryParse(string[] argv, out Cli cli, out string? error) {
        cli = new Cli();
        error = null;

        string? target = null, arg = null, address = null, op = null;
        int? warmup = null, duration = null, concurrency = null, count = null;

        for (int i = 0; i < argv.Length; i++) {
            switch (argv[i]) {
                case "-target":
                    if (!TryValue(argv, ref i, "-target", out target, out error)) return false;
                    break;

                case "-arg":
                    if (!TryValue(argv, ref i, "-arg", out arg, out error)) return false;
                    break;

                case "-address":
                    if (!TryValue(argv, ref i, "-address", out address, out error)) return false;
                    break;

                case "-op":
                    if (!TryValue(argv, ref i, "-op", out op, out error)) return false;
                    break;

                case "-n":
                    if (!TryValue(argv, ref i, "-n", out string? n, out error)) return false;
                    if (!int.TryParse(n, CultureInfo.InvariantCulture, out int parsedCount) || parsedCount < 0) {
                        error = $"-n must be an integer >= 0 (got '{n}')";
                        return false;
                    }
                    count = parsedCount;
                    break;

                case "-w":
                    if (!TryValue(argv, ref i, "-w", out string? w, out error)) return false;
                    if (!TrySeconds(w, "-w", allowZero: true, out warmup, out error)) return false;
                    break;

                case "-d":
                    if (!TryValue(argv, ref i, "-d", out string? d, out error)) return false;
                    if (!TrySeconds(d, "-d", allowZero: true, out duration, out error)) return false;
                    break;

                case "-c":
                    if (!TryValue(argv, ref i, "-c", out string? c, out error)) return false;
                    if (!int.TryParse(c, CultureInfo.InvariantCulture, out int parsed) || parsed < 1) {
                        error = $"-c must be an integer >= 1 (got '{c}')";
                        return false;
                    }
                    concurrency = parsed;
                    break;

                default:
                    error = $"Unknown argument: {argv[i]}";
                    return false;
            }
        }

        switch (target) {
            case "wcf":
                if (arg is not ("basichttp" or "wshttp" or "nettcp")) {
                    error = $"-arg '{arg}' is not a valid WCF transport (expected basichttp, wshttp or nettcp)";
                    return false;
                }
                cli.Transport = arg switch {
                    "basichttp" => WcfTransport.BasicHttp,
                    "wshttp" => WcfTransport.WsHttp,
                    _ => WcfTransport.NetTcp
                };
                cli.Address = address ?? cli.Transport switch {
                    WcfTransport.BasicHttp => BasicHttpAddress,
                    WcfTransport.WsHttp => WsHttpAddress,
                    _ => NetTcpAddress
                };
                break;

            case "grpc":
                if (arg is not null) {
                    error = $"-arg '{arg}' is not valid with -target grpc";
                    return false;
                }
                cli.UseGrpc = true;
                cli.Address = address
                              ?? (Environment.GetEnvironmentVariable("BENCH_GRPC_URL") is { Length: > 0 } url ? url : GrpcAddress);
                break;

            case null:
                error = "-target is required";
                return false;

            default:
                error = $"Unknown -target '{target}' (expected wcf or grpc)";
                return false;
        }

        if (op is not null) {
            switch (op) {
                case "getorderbyid":
                    cli.Operation = BenchOperation.GetOrderById;
                    break;
                case "getorders":
                    cli.Operation = BenchOperation.GetOrders;
                    break;
                default:
                    error = $"-op '{op}' is not a valid operation (expected getorderbyid or getorders)";
                    return false;
            }
        }

        // -n only means something for the batch operation. Catching it here beats silently
        // ignoring a count the run never sends.
        if (count is not null) {
            if (cli.Operation != BenchOperation.GetOrders) {
                error = "-n is only valid with '-op getorders'";
                return false;
            }
            cli.Count = count.Value;
        }

        cli.Warmup = warmup;
        cli.Duration = duration;
        cli.Concurrency = concurrency ?? 1;
        return true;
    }

    static bool TryValue(string[] argv, ref int i, string option, [NotNullWhen(true)] out string? value, out string? error) {
        if (i + 1 >= argv.Length) {
            value = null;
            error = $"{option} requires a value";
            return false;
        }
        value = argv[++i];
        error = null;
        return true;
    }

    static bool TrySeconds(string value, string option, bool allowZero, out int? seconds, out string? error) {
        if (int.TryParse(value, CultureInfo.InvariantCulture, out int parsed) && (allowZero || parsed > 0)) {
            seconds = parsed;
            error = null;
            return true;
        }
        seconds = null;
        error = $"{option} must be an integer number of seconds >= 0 (got '{value}')";
        return false;
    }

    public static void PrintUsage(TextWriter writer) {
        writer.WriteLine("Usage:");
        writer.WriteLine("  Bench.Client -target wcf  -arg basichttp");
        writer.WriteLine("  Bench.Client -target wcf  -arg wshttp");
        writer.WriteLine("  Bench.Client -target wcf  -arg nettcp");
        writer.WriteLine("  Bench.Client -target grpc");
        writer.WriteLine();
        writer.WriteLine("Operations:");
        writer.WriteLine("  -op getorderbyid   single order, the default (backwards compatible)");
        writer.WriteLine("  -op getorders      batch of orders, 10000 by default");
        writer.WriteLine();
        writer.WriteLine("Benchmark options (omit -w/-d/-c for a single-call smoke test):");
        writer.WriteLine("  -w <seconds>  warmup duration before measurement starts (default 0)");
        writer.WriteLine("  -d <seconds>  measurement duration (default 0 = single call)");
        writer.WriteLine("  -c <count>    concurrent workers (default 1)");
        writer.WriteLine();
        writer.WriteLine("Batch options (-op getorders only):");
        writer.WriteLine("  -n <count>    orders per call; 0 or omitted means the server default (10000),");
        writer.WriteLine("                clamped server side to 1000000");
        writer.WriteLine();
        writer.WriteLine("Overrides:");
        writer.WriteLine("  -address <uri>       override the endpoint address for the selected target");
        writer.WriteLine("  BENCH_GRPC_URL       override the gRPC address for -target grpc");
    }
}
