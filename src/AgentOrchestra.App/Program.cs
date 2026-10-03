using AgentOrchestra.App.Agents;
using AgentOrchestra.App.Domain;
using AgentOrchestra.App.Guardrails;
using AgentOrchestra.App.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using System.ClientModel;

// LLM_BACKEND: "ollama" (default) or "llamacpp" (llama-server, OpenAI-compatible API).
var backend = (Environment.GetEnvironmentVariable("LLM_BACKEND") ?? "ollama").ToLowerInvariant();
var isLlamaCpp = backend is "llamacpp" or "llama.cpp" or "llama-server";
var baseUrl = Environment.GetEnvironmentVariable("LLM_URL")
    ?? (isLlamaCpp ? "http://localhost:8080" : "http://localhost:11434");
var model = Environment.GetEnvironmentVariable("LLM_MODEL")
    ?? (isLlamaCpp ? "local" : "gemma4:e4b"); // llama-server serves whatever model it was started with
var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY") ?? "none";

IChatClient chatClient = isLlamaCpp
    ? new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl.TrimEnd('/') + "/v1") })
        .GetChatClient(model)
        .AsIChatClient()
    : new OllamaApiClient(new Uri(baseUrl), model); // implements IChatClient

// Semantic guardrail: EMBED_MODEL / EMBED_URL select the embedding model and server,
// GUARD_THRESHOLD overrides the configured threshold, GUARD=off disables it, GUARD_DEBUG=1 prints scores.
SemanticGuard? guard = null;
if (!string.Equals(Environment.GetEnvironmentVariable("GUARD"), "off", StringComparison.OrdinalIgnoreCase))
{
    var embedModel = Environment.GetEnvironmentVariable("EMBED_MODEL") ?? "bge-m3";
    var embedUrl = Environment.GetEnvironmentVariable("EMBED_URL")
        ?? (isLlamaCpp ? "http://localhost:8081" : baseUrl); // llama-server needs its own --embeddings instance
    IEmbeddingGenerator<string, Embedding<float>> embedder = isLlamaCpp
        ? new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(embedUrl.TrimEnd('/') + "/v1") })
            .GetEmbeddingClient(embedModel)
            .AsIEmbeddingGenerator()
        : new OllamaApiClient(new Uri(embedUrl), embedModel);

    float? thresholdOverride = float.TryParse(Environment.GetEnvironmentVariable("GUARD_THRESHOLD"),
        System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : null;

    try
    {
        guard = await SemanticGuard.CreateAsync(embedder,
            Path.Combine(AppContext.BaseDirectory, "Guardrails", "guardrails.json"), embedModel, thresholdOverride);
    }
    catch (Exception ex)
    {
        // Fail closed: without a working guard the prompt is not forwarded.
        Console.Error.WriteLine($"Guardrail unavailable ({embedModel} @ {embedUrl}): {ex.Message}");
        return 3;
    }
}

// 'nimble' scope classifier (Ollama /v1/systemone): NIMBLE_MODEL, NIMBLE_URL, NIMBLE=off disables it
// (the planner agent still rejects out-of-scope questions).
ScopeClassifierAgent? scopeClassifier = null;
if (!string.Equals(Environment.GetEnvironmentVariable("NIMBLE"), "off", StringComparison.OrdinalIgnoreCase))
{
    var nimbleUrl = Environment.GetEnvironmentVariable("NIMBLE_URL")
        ?? (isLlamaCpp ? "http://localhost:11434" : baseUrl); // nimble only exists in Ollama
    var nimbleHttp = new HttpClient { BaseAddress = new Uri(nimbleUrl), Timeout = TimeSpan.FromMinutes(5) };
    scopeClassifier = new ScopeClassifierAgent(
        new NimbleClient(nimbleHttp, Environment.GetEnvironmentVariable("NIMBLE_MODEL") ?? "nimble"));
}

// TODO: replace the mock with the real sample query service (ISampleQueryService) and reference mappings (IReferenceResolver).
// QUERY=off runs without a query service: the workflow then prints the parsed SampleQueryRequest instead of executing it.
ISampleQueryService? queryService =
    string.Equals(Environment.GetEnvironmentVariable("QUERY"), "off", StringComparison.OrdinalIgnoreCase)
        ? null
        : new MockSampleQueryService();

var options = new WorkflowOptions { Debug = Environment.GetEnvironmentVariable("GUARD_DEBUG") is "1" };
var workflow = new AgentWorkflow(guard, scopeClassifier,
    new QueryPlannerAgent(chatClient), new AnswerAgent(chatClient),
    new PlanTranslator(new NumericOnlyReferenceResolver()), queryService, options);

// One-shot: command-line args or piped stdin. Interactive terminal without args: chat loop.
if (args.Length > 0 || Console.IsInputRedirected)
{
    var input = args.Length > 0 ? string.Join(' ', args) : await Console.In.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(input))
    {
        Console.Error.WriteLine("No input text given.");
        return 1;
    }
    return await workflow.RunAsync(input);
}

await new ChatLoop(workflow, options,
    status:
    [
        $"LLM:     {backend}/{model} @ {baseUrl}",
        $"Guard:   {(guard is null ? "off (GUARD=off)" : $"on, threshold {guard.Threshold:F2}")}",
        $"Scope:   {(scopeClassifier is null ? "off (NIMBLE=off)" : "nimble")}",
        $"Query:   {(queryService is null ? "off (QUERY=off), parsed requests are only printed (dry run)" : "mock data")}",
        "Env:     LLM_BACKEND LLM_URL LLM_MODEL LLM_API_KEY EMBED_MODEL EMBED_URL GUARD GUARD_THRESHOLD GUARD_DEBUG NIMBLE NIMBLE_MODEL NIMBLE_URL QUERY",
    ],
    guardAvailable: guard is not null,
    scopeAvailable: scopeClassifier is not null).RunAsync();
return 0;
