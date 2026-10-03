namespace AgentOrchestra.App.Domain;

/// <summary>The request accepted by the sample query. Unset filters mean "no restriction".</summary>
public sealed class SampleQueryRequest
{
    public List<State> States { get; set; } = [];

    // TODO: replace string with the real result type (e.g. an enum for positive/negative) once known.
    public List<string> Results { get; set; } = [];

    public int? AnalyzerId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? CustomerId { get; set; }
    public List<int> AnalysesIds { get; set; } = [];

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
