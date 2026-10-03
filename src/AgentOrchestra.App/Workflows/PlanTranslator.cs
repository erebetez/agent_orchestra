using System.Globalization;
using AgentOrchestra.App.Agents;
using AgentOrchestra.App.Domain;

namespace AgentOrchestra.App.Workflows;

/// <summary>Resolves the names of a <see cref="QueryPlan"/> to ids and builds the <see cref="SampleQueryRequest"/>.</summary>
public sealed class PlanTranslator(IReferenceResolver resolver)
{
    /// <summary>Request, plus the id of the analysis to add (tasks only). Either <c>Error</c> or <c>Request</c> is set.</summary>
    public sealed record Translation(SampleQueryRequest? Request, int? TaskAnalysisId, string? Error);

    public async Task<Translation> TranslateAsync(QueryPlan plan, CancellationToken ct = default)
    {
        var f = plan.Filter;
        var unresolved = new List<string>();

        async Task<int?> Resolve(ReferenceKind kind, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var id = await resolver.ResolveAsync(kind, name, ct);
            if (id is null) unresolved.Add($"{kind.ToString().ToLowerInvariant()} '{name}'");
            return id;
        }

        var analysisIds = new List<int>();
        foreach (var name in f.Analyses ?? [])
            if (await Resolve(ReferenceKind.Analysis, name) is { } id) analysisIds.Add(id);

        var request = new SampleQueryRequest
        {
            States = (f.States ?? []).Where(s => s != State.All).Distinct().ToList(),
            Results = (f.Results ?? []).Select(r => r.Trim().ToLowerInvariant()).Distinct().ToList(),
            AnalyzerId = await Resolve(ReferenceKind.Analyzer, f.Analyzer),
            CustomerId = await Resolve(ReferenceKind.Customer, f.Customer),
            AnalysesIds = analysisIds,
        };

        if (ParseDate(f.From) is { } from) request.From = from.ToDateTime(TimeOnly.MinValue);
        if (ParseDate(f.To) is { } to) request.To = to.ToDateTime(TimeOnly.MaxValue); // inclusive end of day

        int? taskAnalysisId = null;
        if (plan.Intent == Intent.Task)
            taskAnalysisId = plan.Task is { Kind: TaskKind.AddAnalysis, Analysis: { } a }
                ? await Resolve(ReferenceKind.Analysis, a)
                : null;

        if (plan.Intent == Intent.Task && plan.Task?.Analysis is null)
            return new Translation(null, null, "Which analysis should be added?");
        if (unresolved.Count > 0)
            return new Translation(null, null, $"I could not find: {string.Join(", ", unresolved)}.");
        return new Translation(request, taskAnalysisId, null);
    }

    private static DateOnly? ParseDate(string? s)
        => DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
