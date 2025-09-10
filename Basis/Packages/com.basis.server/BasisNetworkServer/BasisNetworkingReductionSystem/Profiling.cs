using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BasisNetworkServer.BasisNetworkingReductionSystem
{
    public static class Profiling
    {
        public static readonly ConcurrentDictionary<string, List<long>> Timings = new();
        private static long lastPrintTicks = Stopwatch.GetTimestamp();
        private static readonly double MsToTick = Stopwatch.Frequency / 1000.0;
        private static readonly long printIntervalTicks = (long)(5000 * MsToTick);

        public static void StartTimer(string key, out long startTicks)
        {
            startTicks = Stopwatch.GetTimestamp();
        }

        public static void EndTimer(string key, long startTicks)
        {
            long duration = Stopwatch.GetTimestamp() - startTicks;
            if (!Timings.ContainsKey(key))
            {
                Timings[key] = new List<long>(1024);
            }

            Timings[key].Add(duration);
        }

        public static void TryPrint()
        {
            long now = Stopwatch.GetTimestamp();
            if (now - lastPrintTicks >= printIntervalTicks)
            {
                BNL.Log("\n[BSR Profiling Summary]");
                foreach (var kvp in Timings)
                {
                    if (kvp.Value.Count == 0) continue;
                    double avgMs = kvp.Value.Average(ticks => ticks / MsToTick);
                    BNL.Log($"{kvp.Key}: {avgMs:F3} ms over {kvp.Value.Count} runs");
                    kvp.Value.Clear(); // reset for next interval
                }

                lastPrintTicks = now;
            }
        }
    }
}
