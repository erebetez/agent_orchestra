using System.Text.Json;
using AgentOrchestra.App.Agents;
using AgentOrchestra.App.Domain;
using AgentOrchestra.App.Guardrails;

namespace AgentOrchestra.App.Workflows;

/// <summary>
/// The flow of a request: semantic guardrail, scope check, planner, then routing by intent:
/// Filter -> raw data, Answer -> short message, Task -> GUI action with the affected sample ids,
/// OutOfScope -> rejection. The response is written to stdout as JSON.
/// Exit code: 0 ok, 2 blocked by the guardrail, 3 a step was unavailable, 4 rejected (out of scope / not understood).
/// </summary>
public sealed class AgentWorkflow(
    SemanticGuard? guard,
    ScopeClassifierAgent? scopeClassifier,
    QueryPlannerAgent planner,
    AnswerAgent answerer,
    PlanTranslator translator,
    ISampleQueryService? queryService,
    WorkflowOptions options)
{
    private const int TaskPageSize = 500;
    private const int MaxTaskSamples = 10_000;
    private const int AnswerItems = 20;

    public async Task<int> RunAsync(string input)
    {
        var timings = new Timings();
        try { return await RunCoreAsync(input, timings); }
        finally { if (options.Timings) timings.Print(Console.Error); }
    }

    private async Task<int> RunCoreAsync(string input, Timings timings)
    {
        if (guard is not null && options.Guard)
        {
            GuardResult verdict;
            try { verdict = await timings.MeasureAsync("guardrail", () => guard.CheckAsync(input)); }
            catch (Exception ex)
            {
                // Fail closed: without a working guard the prompt is not forwarded.
                Console.Error.WriteLine($"Guardrail unavailable: {ex.Message}");
                return 3;
            }

            if (options.Debug)
                Console.Error.WriteLine($"[guard] best score {verdict.Score:F3} (threshold {guard.Threshold:F2})");
            if (verdict.Blocked)
            {
                Console.Error.WriteLine($"Blocked by guardrail '{verdict.Category}' (score {verdict.Score:F2}, similar to: \"{verdict.Phrase}\").");
                return 2;
            }
        }

        if (scopeClassifier is not null && options.Scope)
        {
            double score;
            try { score = await timings.MeasureAsync("scope classifier", () => scopeClassifier.ScoreAsync(input)); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Scope classifier unavailable: {ex.Message}");
                return 3;
            }
            if (options.Debug)
                Console.Error.WriteLine($"[scope] in-scope probability {score:F3} (threshold {ScopeClassifierAgent.Threshold:F2})");
            if (score < ScopeClassifierAgent.Threshold) return Emit(OutOfScope());
        }

        QueryPlan plan;
        try { plan = await timings.MeasureAsync("planner", () => planner.PlanAsync(input, DateOnly.FromDateTime(DateTime.Today))); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Planner failed: {ex.Message}");
            return 3;
        }

        if (options.Debug)
            Console.Error.WriteLine($"[plan] {JsonSerializer.Serialize(plan, QueryPlan.Json)}");

        if (plan.Intent == Intent.OutOfScope) return Emit(OutOfScope(plan.Reason));

        var translation = await translator.TranslateAsync(plan);
        if (options.Debug)
            Console.Error.WriteLine($"[request] {JsonSerializer.Serialize(translation, QueryPlan.Json)}");
        if (translation.Request is not { } request)
            return Emit(new WorkflowResponse(ResponseKind.Rejected, translation.Error));

        if (queryService is null)
            return Emit(new WorkflowResponse(ResponseKind.DryRun,
                $"Intent: {plan.Intent}. No query service configured, nothing was executed.", request,
                Action: plan.Intent == Intent.Task ? new GuiAction(nameof(TaskKind.AddAnalysis), translation.TaskAnalysisId, []) : null));

        try
        {
            return Emit(plan.Intent switch
            {
                Intent.Filter => new WorkflowResponse(ResponseKind.Data, Request: request,
                    Data: await timings.MeasureAsync("query", () => queryService.QueryAsync(request))),
                Intent.Answer => await AnswerAsync(input, request, timings),
                Intent.Task => await TaskAsync(request, translation.TaskAnalysisId!.Value, timings),
                _ => OutOfScope(plan.Reason),
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Query failed: {ex.Message}");
            return 3;
        }
    }

    private async Task<WorkflowResponse> AnswerAsync(string input, SampleQueryRequest request, Timings timings)
    {
        request.PageSize = AnswerItems; // the count comes from TotalCount, only a few rows as context
        var result = await timings.MeasureAsync("query", () => queryService!.QueryAsync(request));
        var message = await timings.MeasureAsync("answerer", () => answerer.AnswerAsync(input, request, result));
        return new WorkflowResponse(ResponseKind.Message, message);
    }

    private async Task<WorkflowResponse> TaskAsync(SampleQueryRequest request, int analysisId, Timings timings)
    {
        // Collect the ids of all matching samples (all pages).
        var ids = new List<int>();
        request.PageSize = TaskPageSize;
        await timings.MeasureAsync("query", async () =>
        {
            for (request.Page = 1; ids.Count < MaxTaskSamples; request.Page++)
            {
                var page = await queryService!.QueryAsync(request);
                ids.AddRange(page.Items.Select(i => i.Id));
                if (page.Items.Count == 0 || ids.Count >= page.TotalCount) break;
            }
            return ids.Count;
        });

        if (ids.Count == 0)
            return new WorkflowResponse(ResponseKind.Message, "No samples match, so there is nothing to change.", request);
        return new WorkflowResponse(ResponseKind.Action,
            $"{ids.Count} samples selected.", request,
            Action: new GuiAction(nameof(TaskKind.AddAnalysis), analysisId, ids));
    }

    private static WorkflowResponse OutOfScope(string? reason = null) => new(ResponseKind.Rejected,
        string.IsNullOrWhiteSpace(reason)
            ? "I can only help with samples: find them, count them, or add analyses to them."
            : reason);

    private static int Emit(WorkflowResponse response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response, QueryPlan.Json));
        return response.ExitCode;
    }
}
