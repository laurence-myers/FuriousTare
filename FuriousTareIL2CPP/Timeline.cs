using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FuriousTareIL2CPP;

/**
 * Records a timeline of coroutine steps and method calls, then logs it as a table. Used by the "*Timings" diagnostic
 * patches.
 *
 * Coroutines: patch the compiler-generated state machine's MoveNext(), and call StepPrefix() and StepPostfix(). Each
 * MoveNext() call is one "step": the synchronous work between two yields. The time until the next step is the "wait"
 * at that yield. (Wrapping an IL2CPP coroutine's IEnumerator doesn't work well, see TakeASwig.)
 *
 * Methods: call HookBegin() in a prefix, and HookEnd() in a postfix, passing the token via Harmony's __state.
 *
 * IL2CPP might inline some methods, in which case their hooks never fire.
 */
public class Timeline
{
    private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

    public readonly struct HookToken
    {
        public readonly long Ticks;
        public readonly int Gcs;

        public HookToken(long ticks, int gcs)
        {
            Ticks = ticks;
            Gcs = gcs;
        }

        public bool IsValid => Ticks != 0;
    }

    private class Entry
    {
        public bool IsStep;
        public string Text;
        public string Detail;
        public long StartTicks;
        public long WorkTicks;
        public int WorkGcs;
        public long WaitTicks;
        public int WaitFrames;
        public int WaitGcs;
        public int Repeats = 1;
        public int Depth;
    }

    private class CoroutineTracker
    {
        public string Name;
        public Func<int, string> ResumeLabel;
        public Func<int, string> YieldLabel;
        public long PrefixTicks;
        public int PrefixGcs;
        public int PrefixState;
        public int PrefixDepth;
        public long LastEndTicks;
        public int LastEndFrame;
        public int LastEndGcs;
        public int LastYieldState = int.MinValue;
        public Entry LastEntry;
    }

    private readonly Dictionary<IntPtr, CoroutineTracker> _trackers = new();
    private List<Entry> _entries;
    private string _title;
    private long _startTicks;
    private int _startFrame;
    private int _startGcs;
    private int _depth;

    public bool IsActive => _entries != null;

    public void Begin(string title)
    {
        Begin(
            title,
            Stopwatch.GetTimestamp(),
            Time.frameCount
        );
    }

    public void Begin(string title, long startTicks, int startFrame)
    {
        _entries = new List<Entry>();
        _trackers.Clear();
        _title = title;
        _startTicks = startTicks;
        _startFrame = startFrame;
        _startGcs = GcCount();
        _depth = 0;
    }

    public void AppendTitle(string text)
    {
        _title += text;
    }

    public void Discard()
    {
        _entries = null;
        _trackers.Clear();
    }

    public void End(string outcome, IEnumerable<string> footer = null)
    {
        var entries = _entries;
        _entries = null;
        _trackers.Clear();
        if (entries == null)
        {
            return;
        }

        var totalTicks = Stopwatch.GetTimestamp() - _startTicks;
        var totalFrames = Time.frameCount - _startFrame;
        var totalGcs = GcCount() - _startGcs;

        var sb = new StringBuilder();
        sb.AppendLine($"{_title} ({outcome})");
        sb.AppendLine(
            $"  Total {Ms(totalTicks)} ms over {totalFrames} frames, {totalGcs} garbage collections. \"gc\" columns: collections during the work / during the wait."
        );
        if (footer != null)
        {
            foreach (var line in footer)
            {
                sb.AppendLine($"  {line}");
            }
        }

        sb.AppendLine("   start ms |  work ms |  gc |  wait ms | frames |  gc |   x | what");
        foreach (var entry in entries.OrderBy(e => e.StartTicks))
        {
            sb.Append($"  {Ms(entry.StartTicks),9} | {Ms(entry.WorkTicks),8} | {Count(entry.WorkGcs),3} | ");
            sb.Append(
                entry.IsStep
                    ? $"{Ms(entry.WaitTicks),8} | {entry.WaitFrames,6} | {Count(entry.WaitGcs),3} | "
                    : "         |        |     | "
            );
            sb.Append(entry.Repeats > 1 ? $"{entry.Repeats,3} | " : "    | ");
            sb.Append(new string(' ', entry.Depth * 2));
            sb.Append(entry.Text);
            if (entry.Detail != null)
            {
                sb.Append($" [{entry.Detail}]");
            }

            sb.AppendLine();
        }

        Logger.Log.LogInfo(sb.ToString());
    }

    public static int GcCount()
    {
        try
        {
            return Il2CppSystem.GC.CollectionCount(0);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string Ms(long ticks) => (ticks * MsPerTick).ToString("0.0");

    private static string Count(int count) => count == 0 ? "" : count.ToString();

    #region Coroutine steps

    public void StepPrefix(
        IntPtr pointer,
        int state,
        Func<string> name,
        Func<int, string> resumeLabel,
        Func<int, string> yieldLabel
    )
    {
        if (!IsActive)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var gcs = GcCount();
        if (!_trackers.TryGetValue(pointer, out var tracker))
        {
            tracker = new CoroutineTracker
            {
                Name = name(),
                ResumeLabel = resumeLabel,
                YieldLabel = yieldLabel,
            };
            _trackers[pointer] = tracker;
        }

        // Attribute the time since the last step to the yield we were waiting on
        if (tracker.LastEntry != null)
        {
            tracker.LastEntry.WaitTicks += now - tracker.LastEndTicks;
            tracker.LastEntry.WaitFrames += Time.frameCount - tracker.LastEndFrame;
            tracker.LastEntry.WaitGcs += gcs - tracker.LastEndGcs;
        }

        tracker.PrefixTicks = now;
        tracker.PrefixGcs = gcs;
        tracker.PrefixState = state;
        tracker.PrefixDepth = _depth++;
    }

    public void StepPostfix(IntPtr pointer, bool hasMore, int stateAfter, string detail = null)
    {
        if (!IsActive || !_trackers.TryGetValue(pointer, out var tracker))
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var gcs = GcCount();
        _depth = tracker.PrefixDepth;
        var yieldState = hasMore ? stateAfter : -1;
        var workTicks = now - tracker.PrefixTicks;
        var workGcs = gcs - tracker.PrefixGcs;

        // Collapse "while (x) yield return ..." loops into one entry
        var isLoopRepeat = tracker.LastEntry != null
                           && tracker.PrefixState == tracker.LastYieldState
                           && yieldState == tracker.PrefixState
                           && tracker.LastEntry.Detail == detail;
        if (isLoopRepeat)
        {
            tracker.LastEntry.Repeats++;
            tracker.LastEntry.WorkTicks += workTicks;
            tracker.LastEntry.WorkGcs += workGcs;
        }
        else
        {
            var entry = new Entry
            {
                IsStep = true,
                Text =
                    $"{tracker.Name}: {tracker.ResumeLabel(tracker.PrefixState)} -> {tracker.YieldLabel(yieldState)}",
                Detail = detail,
                StartTicks = tracker.PrefixTicks - _startTicks,
                WorkTicks = workTicks,
                WorkGcs = workGcs,
                Depth = tracker.PrefixDepth,
            };
            _entries.Add(entry);
            tracker.LastEntry = entry;
        }

        tracker.LastYieldState = yieldState;
        tracker.LastEndTicks = now;
        tracker.LastEndFrame = Time.frameCount;
        tracker.LastEndGcs = gcs;

        if (!hasMore)
        {
            _trackers.Remove(pointer);
        }
    }

    public static string YieldOrEnd(int state) => state < 0 ? "(end)" : "end of frame";

    #endregion

    #region Method calls

    public HookToken HookBegin()
    {
        if (!IsActive)
        {
            return default;
        }

        _depth++;
        return new HookToken(
            Stopwatch.GetTimestamp(),
            GcCount()
        );
    }

    public void HookEnd(HookToken token, string text)
    {
        if (!token.IsValid || !IsActive)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        _depth--;
        _entries.Add(
            new Entry
            {
                Text = text,
                StartTicks = token.Ticks - _startTicks,
                WorkTicks = now - token.Ticks,
                WorkGcs = GcCount() - token.Gcs,
                Depth = _depth,
            }
        );
    }

    public void Mark(string text)
    {
        if (!IsActive)
        {
            return;
        }

        _entries.Add(
            new Entry
            {
                Text = text,
                StartTicks = Stopwatch.GetTimestamp() - _startTicks,
                Depth = _depth,
            }
        );
    }

    #endregion
}
