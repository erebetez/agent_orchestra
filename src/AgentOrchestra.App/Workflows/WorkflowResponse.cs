using System.Text.Json.Serialization;
using AgentOrchestra.App.Domain;

namespace AgentOrchestra.App.Workflows;

public enum ResponseKind
{
    /// <summary>A short answer for the user.</summary>
    Message,
    /// <summary>Raw filter result.</summary>
    Data,
    /// <summary>Signal for the GUI to open a workflow.</summary>
    Action,
    /// <summary>The question is outside the supported scope or could not be understood.</summary>
    Rejected,
    /// <summary>Query service not wired yet: the request that would have been sent.</summary>
    DryRun
}

public sealed record GuiAction(string Type, int? AnalysisId, IReadOnlyList<int> SampleIds);

public sealed record WorkflowResponse(
    ResponseKind Kind,
    string? Message = null,
    SampleQueryRequest? Request = null,
    SampleQueryResult? Data = null,
    GuiAction? Action = null)
{
    [JsonIgnore] public int ExitCode => Kind == ResponseKind.Rejected ? 4 : 0;
}
