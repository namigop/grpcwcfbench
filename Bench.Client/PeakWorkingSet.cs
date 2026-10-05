using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bench.Client;

static class PeakWorkingSet {
    const int RusageSelf = 0;
    
    public static long Bytes => OperatingSystem.IsWindows()
        ? Process.GetCurrentProcess().PeakWorkingSet64
        : RusageMaxRssBytes();

    /// <summary>
    /// <c>ru_maxrss</c> is in bytes on Darwin, in kilobytes on Linux and the other BSDs.
    /// </summary>
    static long RusageMaxRssBytes() {
        if (getrusage(RusageSelf, out RUsage usage) != 0) return 0;

        bool reportedInBytes = OperatingSystem.IsMacOS()
                               || OperatingSystem.IsMacCatalyst()
                               || OperatingSystem.IsIOS();
        return reportedInBytes ? usage.ru_maxrss : usage.ru_maxrss * 1024;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TimeVal {
        public long tv_sec;
        public long tv_usec;
    }

    /// <summary>
    /// The native <c>struct rusage</c> has to be declared in full, not just through
    /// <c>ru_maxrss</c>: <c>getrusage</c> writes the whole 144-byte structure, so a short
    /// declaration would let the native side write past the end of the managed buffer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct RUsage {
        public TimeVal ru_utime;
        public TimeVal ru_stime;
        public long ru_maxrss;
        public long ru_ixrss;
        public long ru_idrss;
        public long ru_isrss;
        public long ru_minflt;
        public long ru_majflt;
        public long ru_nswap;
        public long ru_inblock;
        public long ru_oublock;
        public long ru_msgsnd;
        public long ru_msgrcv;
        public long ru_nsignals;
        public long ru_nvcsw;
        public long ru_nivcsw;
    }

    [DllImport("libc", EntryPoint = "getrusage", SetLastError = true)]
    static extern int getrusage(int who, out RUsage usage);
}
