using System.Diagnostics;

namespace AgentOrchestra.App.Workflows;

/// <summary>Collects how long each workflow step took and prints a summary.</summary>
public sealed class Timings
{
    private readonly List<(string Step, TimeSpan Elapsed, string? Detail)> _entries = [];
    private readonly Stopwatch _total = Stopwatch.StartNew();

    public async Task<T> MeasureAsync<T>(string step, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try { return await action(); }
        finally { Add(step, sw.Elapsed); }
    }

    public void Add(string step, TimeSpan elapsed, string? detail = null) => _entries.Add((step, elapsed, detail));

    public void Print(TextWriter writer)
    {
        if (_entries.Count == 0) return;
        writer.WriteLine();
        writer.WriteLine("Timings:");
        var width = _entries.Max(e => e.Step.Length);
        foreach (var (step, elapsed, detail) in _entries)
            writer.WriteLine($"  {step.PadRight(width)}  {Format(elapsed),8}{(detail is null ? "" : $"  ({detail})")}");
        writer.WriteLine($"  {"total".PadRight(width)}  {Format(_total.Elapsed),8}");
    }

    private static string Format(TimeSpan t) => t.TotalSeconds >= 1 ? $"{t.TotalSeconds:F2} s" : $"{t.TotalMilliseconds:F0} ms";
}
