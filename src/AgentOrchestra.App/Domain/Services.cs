namespace AgentOrchestra.App.Domain;

// TODO: placeholder row shape, replace with the real sample DTO.
public sealed record SampleRow(int Id, string? IsbtCode, State State, string? Result);

public sealed record SampleQueryResult(int TotalCount, IReadOnlyList<SampleRow> Items);

/// <summary>The sample query tool. When absent, the workflow only reports the request it would send.</summary>
public interface ISampleQueryService
{
    Task<SampleQueryResult> QueryAsync(SampleQueryRequest request, CancellationToken ct = default);
}

public enum ReferenceKind { Customer, Analyzer, Analysis }

/// <summary>Maps a name used in a question (customer, analyzer, analysis, ...) to its id.</summary>
public interface IReferenceResolver
{
    /// <summary>Returns null when the name is unknown.</summary>
    Task<int?> ResolveAsync(ReferenceKind kind, string name, CancellationToken ct = default);
}

/// <summary>Until the real mappings exist only numeric ids can be resolved.</summary>
public sealed class NumericOnlyReferenceResolver : IReferenceResolver
{
    public Task<int?> ResolveAsync(ReferenceKind kind, string name, CancellationToken ct = default)
        => Task.FromResult<int?>(int.TryParse(name.Trim(), out var id) ? id : null);
}
