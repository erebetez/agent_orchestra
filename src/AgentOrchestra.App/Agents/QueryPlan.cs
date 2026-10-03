using System.Text.Json;
using System.Text.Json.Serialization;
using AgentOrchestra.App.Domain;

namespace AgentOrchestra.App.Agents;

public enum Intent
{
    /// <summary>The user wants a list of samples (a filter selection).</summary>
    Filter,
    /// <summary>The question has a short answer (a count, yes/no, a single value).</summary>
    Answer,
    /// <summary>The user wants an action performed on the matching samples.</summary>
    Task,
    OutOfScope
}

public enum TaskKind { AddAnalysis }

/// <summary>Filters as written in the question. Names are resolved to ids by the workflow.</summary>
public sealed record PlanFilter(
    State[] States,
    string[] Results,
    string? Analyzer,
    string? From,
    string? To,
    string? Customer,
    string[] Analyses);

public sealed record PlanTask(TaskKind Kind, string? Analysis);

/// <summary>Structured output of the planner agent.</summary>
public sealed record QueryPlan(Intent Intent, string? Reason, PlanFilter Filter, PlanTask? Task)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };
}
