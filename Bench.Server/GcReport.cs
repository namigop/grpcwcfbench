using System.Runtime;

namespace Bench;
 
static class GcReport {
    public static void Print(string label) {
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        long committed = info.TotalCommittedBytes;
        double frag = committed > 0 ? info.FragmentedBytes / (double)committed : 0;

        Console.WriteLine(
            $"GC {label,-6} " +
            $"alloc={Bytes(GC.GetTotalAllocatedBytes(precise: false))} " +
            $"col={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} " +
            $"heap={Bytes(info.HeapSizeBytes)} committed={Bytes(committed)} " +
            $"frag={frag:P1} lastGCpauses={info.PauseDurations.Length} " +
            $"serverGC={GCSettings.IsServerGC}");
    }
    
    static string Bytes(long n) => n switch {
        >= 1L << 30 => $"{n / (double)(1L << 30):0.00} GB",
        >= 1L << 20 => $"{n / (double)(1L << 20):0.00} MB",
        >= 1L << 10 => $"{n / (double)(1L << 10):0.0} KB",
        _ => $"{n:N0} B"
    };
}