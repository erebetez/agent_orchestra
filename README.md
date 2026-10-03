# AgentOrchestra

AgentOrchestra is a small demo project that shows an agent-driven workflow for turning a natural-language question into a structured query and then answering it from a mock dataset.

This repository is intentionally a sample, not a production-ready analytics backend. The current implementation uses in-memory mock data instead of a real database or API, which makes it easy to run locally and experiment with the orchestration flow.

## What it does

The app:

- accepts a plain-language question from the command line or interactive chat loop
- classifies whether the question is in scope
- asks an LLM to plan the query
- translates the plan into a structured `SampleQueryRequest`
- runs a mock query service that filters sample records
- returns a concise answer based on the results

The built-in mock query service contains deterministic sample data for laboratory-style records such as sample IDs, states, result values, customer IDs, analyzer IDs, and analysis IDs.

## Project structure

- `src/AgentOrchestra.App/` — the executable app
- `Domain/` — sample request/result models and mock query service
- `Agents/` — planner and answer agents
- `Workflows/` — orchestration logic and prompt translation
- `Guardrails/` — semantic guardrail configuration

## Prerequisites

- .NET SDK 10
- An LLM backend such as Ollama, or an OpenAI-compatible local server (for example llama.cpp)

The app defaults to Ollama at `http://localhost:11434` and uses the model `gemma4:e4b` unless overridden.

## Quick start

1. Start your local LLM backend.
2. Run the app:

```bash
dotnet run --project src/AgentOrchestra.App -- "show me positive samples for customer 2"
```

This will run the workflow using the built-in mock data service.

## Interactive mode

Run the app without arguments to start the chat loop:

```bash
dotnet run --project src/AgentOrchestra.App
```

Then enter questions like:

```text
show me all validated samples
what were the last 10 measured samples?
find positive results from analyzer 1 in the last 30 days
```

## Dry-run mode

If you want to see the structured query without executing the mock backend, disable the query service:

```bash
QUERY=off dotnet run --project src/AgentOrchestra.App -- "show me all done samples"
```

This prints the parsed request instead of running the mock query.

## Environment variables

The app supports configuration through environment variables:

```bash
LLM_BACKEND=ollama
LLM_URL=http://localhost:11434
LLM_MODEL=gemma4:e4b
LLM_API_KEY=none

GUARD=on
GUARD_THRESHOLD=0.7

NIMBLE=on
QUERY=on
```

You can also switch the backend to llama.cpp or another OpenAI-compatible service by setting `LLM_BACKEND=llamacpp` and adjusting `LLM_URL` and `LLM_MODEL`.

## Notes

- This project uses mock data only; there is no real persistence layer.
- The mock service is designed as a placeholder and can be replaced with a real query implementation later.
- The sample request currently models common filters such as state, result, analyzer, customer, dates, analyses, and paging.

## Example usage

```bash
LLM_BACKEND=ollama \
LLM_MODEL=gemma4:e4b \
LLM_URL=http://localhost:11434 \
dotnet run --project src/AgentOrchestra.App -- "show me negative samples from analyzer 2 created this month"
```

This example demonstrates the intended usage pattern: ask a natural-language question, let the agent plan it, and answer from the included mock dataset.
