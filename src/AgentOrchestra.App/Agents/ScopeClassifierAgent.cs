namespace AgentOrchestra.App.Agents;

/// <summary>Cheap first routing step: is the text about samples at all? Everything else is rejected before the main model runs.</summary>
public sealed class ScopeClassifierAgent(NimbleClient nimble)
{
    private const string Key = "in_scope";

    private static readonly Dictionary<string, NimbleQuestion> Questions = new()
    {
        [Key] = new NimbleQuestion(
            "Does the state text ask to find, count, check or change laboratory samples, pools, analyses, " +
            "analyzers, methods, customers or sample states (for example: show, list, how many, add an analysis)?",
            new Dictionary<string, string>
            {
                ["true"] = "The state text is a request about laboratory samples and their analyses.",
                ["false"] = "The state text is unrelated to laboratory samples (small talk, general knowledge, coding, ...).",
            }),
    };

    public const double Threshold = 0.5;

    /// <summary>Probability (0..1) that the text is about samples.</summary>
    public async Task<double> ScoreAsync(string input, CancellationToken ct = default)
        => (await nimble.AskAsync(input, Questions, ct))[Key];
}
