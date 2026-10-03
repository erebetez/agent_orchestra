using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentOrchestra.App.Agents;

/// <summary>A yes/no question ("noul" = probability 0..1 that the first criterion holds).</summary>
public sealed record NimbleQuestion(
    string Instructions,
    Dictionary<string, string> Criteria,
    string Type = "noul");

/// <summary>Client for the 'nimble' classifier / decision model: Ollama's POST /v1/systemone endpoint.</summary>
public sealed class NimbleClient(HttpClient http, string model)
{
    private sealed record Request(string Model, string State, Dictionary<string, NimbleQuestion> Questions);

    private sealed record Answer(string Type, double? Noul);

    private sealed record Response(Dictionary<string, Answer> Answers);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Returns, per question key, the probability (0..1) that the "true" criterion holds.</summary>
    public async Task<Dictionary<string, double>> AskAsync(
        string state, Dictionary<string, NimbleQuestion> questions, CancellationToken ct = default)
    {
        using var httpResponse = await http.PostAsJsonAsync("/v1/systemone", new Request(model, state, questions), Json, ct);
        httpResponse.EnsureSuccessStatusCode();
        var response = await httpResponse.Content.ReadFromJsonAsync<Response>(Json, ct)
                       ?? throw new InvalidDataException("Empty response from nimble.");

        return questions.Keys.ToDictionary(
            key => key,
            key => response.Answers.TryGetValue(key, out var a) && a.Noul is { } p
                ? p
                : throw new InvalidDataException($"nimble returned no answer for '{key}'."));
    }
}
