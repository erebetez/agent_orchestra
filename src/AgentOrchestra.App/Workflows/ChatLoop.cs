namespace AgentOrchestra.App.Workflows;

/// <summary>
/// Interactive loop: one question per line, each run through the <see cref="AgentWorkflow"/>.
/// Lines starting with '/' are commands that change <see cref="WorkflowOptions"/> or show help.
/// </summary>
public sealed class ChatLoop(AgentWorkflow workflow, WorkflowOptions options, IReadOnlyList<string> status, bool guardAvailable, bool scopeAvailable)
{
    private sealed record Command(string Name, string Help);

    private static readonly Command[] Commands =
    [
        new("/help", "show this help"),
        new("/debug [on|off]", "guard score, scope probability, planner plan and translated request (stderr)"),
        new("/timings [on|off]", "per-step timings after each question"),
        new("/guard [on|off]", "semantic guardrail"),
        new("/scope [on|off]", "nimble scope check (the planner still rejects out-of-scope questions)"),
        new("/status", "show the current settings"),
        new("/quit", "leave (also: /exit, Ctrl+D)"),
    ];

    public async Task RunAsync()
    {
        PrintBanner();
        while (true)
        {
            Console.Write("\n> ");
            var line = Console.ReadLine();
            if (line is null) break; // Ctrl+D
            line = line.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('/'))
            {
                if (!HandleCommand(line)) break;
                continue;
            }

            try { await workflow.RunAsync(line); }
            catch (Exception ex) { Console.Error.WriteLine($"Unexpected error: {ex.Message}"); }
        }
    }

    private void PrintBanner()
    {
        Console.WriteLine("AgentOrchestra: ask about samples (filter, count, add an analysis).");
        Console.WriteLine("Examples: \"show in progress samples from the last week\"");
        Console.WriteLine("          \"how many pools are positive today\"");
        Console.WriteLine();
        foreach (var line in status) Console.WriteLine(line);
        Console.WriteLine();
        PrintHelp();
        PrintSettings();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        var width = Commands.Max(c => c.Name.Length);
        foreach (var c in Commands) Console.WriteLine($"  {c.Name.PadRight(width)}  {c.Help}");
    }

    private void PrintSettings()
    {
        Console.WriteLine("Settings: " + string.Join(", ",
            $"debug {OnOff(options.Debug)}",
            $"timings {OnOff(options.Timings)}",
            $"guard {(guardAvailable ? OnOff(options.Guard) : "unavailable")}",
            $"scope {(scopeAvailable ? OnOff(options.Scope) : "unavailable")}"));
    }

    /// <summary>Returns false to leave the loop.</summary>
    private bool HandleCommand(string line)
    {
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var arg = parts.Length > 1 ? parts[1].ToLowerInvariant() : null;

        switch (parts[0].ToLowerInvariant())
        {
            case "/quit" or "/exit": return false;
            case "/help" or "/?": PrintHelp(); break;
            case "/status": PrintSettings(); break;
            case "/debug": options.Debug = Toggle(options.Debug, arg); PrintSettings(); break;
            case "/timings": options.Timings = Toggle(options.Timings, arg); PrintSettings(); break;
            case "/guard" when !guardAvailable: Console.WriteLine("The guardrail was not initialised at startup (GUARD=off)."); break;
            case "/guard": options.Guard = Toggle(options.Guard, arg); PrintSettings(); break;
            case "/scope" when !scopeAvailable: Console.WriteLine("The scope check was not initialised at startup (NIMBLE=off)."); break;
            case "/scope": options.Scope = Toggle(options.Scope, arg); PrintSettings(); break;
            default: Console.WriteLine($"Unknown command '{parts[0]}'. Type /help."); break;
        }
        return true;
    }

    private static bool Toggle(bool current, string? arg) => arg switch
    {
        "on" or "true" or "1" => true,
        "off" or "false" or "0" => false,
        _ => !current,
    };

    private static string OnOff(bool value) => value ? "on" : "off";
}
