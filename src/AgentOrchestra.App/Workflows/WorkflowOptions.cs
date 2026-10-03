namespace AgentOrchestra.App.Workflows;

/// <summary>Switches that can be changed while the program is running (see the chat loop commands).</summary>
public sealed class WorkflowOptions
{
    /// <summary>Print guard score, scope probability, the planner's plan and the translated request to stderr.</summary>
    public bool Debug { get; set; }

    /// <summary>Print the per-step timings after each question.</summary>
    public bool Timings { get; set; } = true;

    /// <summary>Run the semantic guardrail (only possible when it was initialised at startup).</summary>
    public bool Guard { get; set; } = true;

    /// <summary>Run the nimble scope check (only possible when it was initialised at startup).</summary>
    public bool Scope { get; set; } = true;
}
