namespace AgentOrchestra.App.Domain;

/// <summary>Dummy search backend: a fixed, deterministic set of samples filtered in memory. Replace with the real service.</summary>
public sealed class MockSampleQueryService : ISampleQueryService
{
    private sealed record MockSample(
        int Id, string IsbtCode, State State, string? Result, DateTime Created,
        int CustomerId, int AnalyzerId, int[] AnalysesIds);

    private static readonly State[] States =
        [State.Requested, State.Created, State.Prepared, State.InProgress, State.Measured, State.Done, State.Validated, State.Canceled];

    private static readonly List<MockSample> Samples = Enumerable.Range(1, 120).Select(i =>
    {
        var state = States[i % States.Length];
        var measured = state is State.Measured or State.Done or State.Validated;
        return new MockSample(
            Id: 1000 + i,
            IsbtCode: $"=A{9000 + i * 7:D5}",
            State: state,
            Result: measured ? (i % 3 == 0 ? "positive" : "negative") : null,
            Created: DateTime.Today.AddDays(-(i % 30)).AddHours(8 + i % 9),
            CustomerId: 1 + i % 4,
            AnalyzerId: 1 + i % 3,
            AnalysesIds: [1 + i % 5, 1 + (i + 2) % 5]);
    }).ToList();

    public Task<SampleQueryResult> QueryAsync(SampleQueryRequest r, CancellationToken ct = default)
    {
        var q = Samples.AsEnumerable();
        if (r.States.Count > 0) q = q.Where(s => r.States.Contains(s.State));
        if (r.Results.Count > 0) q = q.Where(s => s.Result is not null && r.Results.Contains(s.Result));
        if (r.AnalyzerId is { } analyzer) q = q.Where(s => s.AnalyzerId == analyzer);
        if (r.CustomerId is { } customer) q = q.Where(s => s.CustomerId == customer);
        if (r.From is { } from) q = q.Where(s => s.Created >= from);
        if (r.To is { } to) q = q.Where(s => s.Created <= to);
        if (r.AnalysesIds.Count > 0) q = q.Where(s => r.AnalysesIds.All(s.AnalysesIds.Contains));

        var all = q.OrderBy(s => s.Id).ToList();
        var pageSize = Math.Max(1, r.PageSize);
        var items = all.Skip((Math.Max(1, r.Page) - 1) * pageSize).Take(pageSize)
            .Select(s => new SampleRow(s.Id, s.IsbtCode, s.State, s.Result)).ToList();
        return Task.FromResult(new SampleQueryResult(all.Count, items));
    }
}
