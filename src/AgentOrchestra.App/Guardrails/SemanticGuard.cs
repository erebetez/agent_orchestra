using System.Numerics.Tensors;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentOrchestra.App.Guardrails;

public sealed record GuardResult(bool Blocked, string? Category, string? Phrase, float Score)
{
    public static GuardResult Allowed(float score) => new(false, null, null, score);
}

/// <summary>
/// Embeds the incoming prompt and compares it (cosine similarity) against embedded reference
/// phrases of unwanted prompts. Blocks when the closest phrase scores at or above the threshold.
/// </summary>
public sealed class SemanticGuard
{
    private sealed record Reference(string Category, string Phrase, ReadOnlyMemory<float> Vector);

    private sealed record Config(float Threshold, Dictionary<string, string[]> Categories);

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder;
    private readonly IReadOnlyList<Reference> _references;

    public float Threshold { get; }

    private SemanticGuard(IEmbeddingGenerator<string, Embedding<float>> embedder,
                          IReadOnlyList<Reference> references, float threshold)
    {
        _embedder = embedder;
        _references = references;
        Threshold = threshold;
    }

    /// <param name="configPath">guardrails.json: { "threshold": 0.7, "categories": { "name": ["phrase", ...] } }</param>
    /// <param name="modelId">Identifies the embedding model; part of the cache key.</param>
    /// <param name="thresholdOverride">Takes precedence over the threshold in the file.</param>
    public static async Task<SemanticGuard> CreateAsync(
        IEmbeddingGenerator<string, Embedding<float>> embedder, string configPath, string modelId,
        float? thresholdOverride = null, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(configPath, ct);
        var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                     ?? throw new InvalidDataException($"Invalid guardrail config: {configPath}");

        var entries = config.Categories
            .SelectMany(c => c.Value.Select(p => (Category: c.Key, Phrase: p)))
            .ToList();

        // Vectors are cached on disk, keyed by model + phrase set, so startup only embeds on changes.
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            modelId + "\n" + string.Join('\n', entries.Select(e => e.Phrase)))));
        var cachePath = Path.ChangeExtension(configPath, ".cache.json");

        float[][]? vectors = TryReadCache(cachePath, key);
        if (vectors is null || vectors.Length != entries.Count)
        {
            var embeddings = await embedder.GenerateAsync(entries.Select(e => e.Phrase).ToList(), cancellationToken: ct);
            vectors = embeddings.Select(e => e.Vector.ToArray()).ToArray();
            File.WriteAllText(cachePath, JsonSerializer.Serialize(new CacheFile(key, vectors)));
        }

        var references = entries
            .Select((e, i) => new Reference(e.Category, e.Phrase, vectors[i]))
            .ToList();
        return new SemanticGuard(embedder, references, thresholdOverride ?? config.Threshold);
    }

    public async Task<GuardResult> CheckAsync(string input, CancellationToken ct = default)
    {
        var embeddings = await _embedder.GenerateAsync([input], cancellationToken: ct);
        var vector = embeddings[0].Vector;

        Reference? best = null;
        var bestScore = float.MinValue;
        foreach (var reference in _references)
        {
            var score = TensorPrimitives.CosineSimilarity(vector.Span, reference.Vector.Span);
            if (score > bestScore) { bestScore = score; best = reference; }
        }

        return best is not null && bestScore >= Threshold
            ? new GuardResult(true, best.Category, best.Phrase, bestScore)
            : GuardResult.Allowed(bestScore);
    }

    private sealed record CacheFile(string Key, float[][] Vectors);

    private static float[][]? TryReadCache(string path, string key)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var cache = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(path));
            return cache?.Key == key ? cache.Vectors : null;
        }
        catch (JsonException) { return null; }
    }
}
