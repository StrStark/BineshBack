using System.Text.Json;
using Binesh.Ai.Orchestration;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Ai.Tools;
using Binesh.Application.Exceptions;
using OpenAI.Chat;

namespace Binesh.Ai.IntegrationTests.Orchestration;

/// <summary>
/// Unit tests for <see cref="AiOrchestrator"/>. The fake
/// <see cref="ScriptedChatClient"/> returns pre-scripted completions in the
/// order they're invoked so we can exercise the tool-call loop without
/// hitting OpenAI.
/// </summary>
public sealed class AiOrchestratorTests
{
    private static readonly Guid UserA = Guid.NewGuid();

    [Fact]
    public async Task NoToolCalls_ReturnsAssistantText()
    {
        var client = new ScriptedChatClient(Text("Hello, there.", inputTokens: 42, outputTokens: 8));
        var registry = BuildRegistry();

        var orchestrator = new AiOrchestrator(client, registry, new InfiniteBudget());
        var result = await orchestrator.RunAsync("hi", UserA, default);

        Assert.Equal("Hello, there.", result.AssistantText);
        Assert.Equal("stop", result.FinishReason);
        Assert.Empty(result.ToolCalls);
        Assert.Equal(50, result.TokensUsed);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task SingleToolCall_Dispatched_AndResultFedBack()
    {
        var args = JsonSerializer.Serialize(new
        {
            Entity = "Customer",
            Select = new { Mode = "list", Fields = new[] { "Mobile" } },
        });

        var client = new ScriptedChatClient(
            ToolCall("call_1", "query_customer", args),
            Text("Found 0 rows."));

        var registry = BuildRegistry();
        var stub = (StubTool)registry.Get("query_customer")!;

        var orchestrator = new AiOrchestrator(client, registry, new InfiniteBudget());
        var result = await orchestrator.RunAsync("Show me customers", UserA, default);

        Assert.Equal("Found 0 rows.", result.AssistantText);
        Assert.Equal("stop", result.FinishReason);
        Assert.Single(result.ToolCalls);
        Assert.Equal("query_customer", result.ToolCalls[0].ToolName);
        Assert.Null(result.ToolCalls[0].Error);
        Assert.Equal(1, stub.ExecuteCallCount);
        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task UnknownTool_RecordedAsErrorAndContinuedTo_Completion()
    {
        var client = new ScriptedChatClient(
            ToolCall("call_x", "query_bogus", "{}"),
            Text("Sorry, I couldn't find that."));

        var orchestrator = new AiOrchestrator(client, BuildRegistry(), new InfiniteBudget());
        var result = await orchestrator.RunAsync("???", UserA, default);

        Assert.Single(result.ToolCalls);
        Assert.NotNull(result.ToolCalls[0].Error);
        Assert.Contains("query_bogus", result.ToolCalls[0].Error!);
        Assert.Equal("Sorry, I couldn't find that.", result.AssistantText);
    }

    [Fact]
    public async Task BadArguments_ErrorSurfacedToModel()
    {
        var client = new ScriptedChatClient(
            ToolCall("call_y", "query_customer", "{ not json"),
            Text("Retrying."));

        var orchestrator = new AiOrchestrator(client, BuildRegistry(), new InfiniteBudget());
        var result = await orchestrator.RunAsync("hi", UserA, default);

        Assert.Single(result.ToolCalls);
        Assert.NotNull(result.ToolCalls[0].Error);
    }

    [Fact]
    public async Task MaxIterations_ReturnsTruncationMessage()
    {
        var args = """{"Entity":"Customer","Select":{"Mode":"list","Fields":["Mobile"]}}""";
        var scripted = Enumerable.Range(0, 10)
            .Select(_ => ToolCall($"call_{Guid.NewGuid()}", "query_customer", args))
            .ToArray();

        var client = new ScriptedChatClient(scripted);
        var orchestrator = new AiOrchestrator(client, BuildRegistry(), new InfiniteBudget());
        var result = await orchestrator.RunAsync("loop", UserA, default);

        Assert.Equal("max_iterations", result.FinishReason);
        Assert.Equal(AiOrchestrator.MaxToolIterations, result.ToolCalls.Count);
        Assert.Equal(AiOrchestrator.MaxToolIterations, client.CallCount);
    }

    [Fact]
    public async Task TokenBudgetExhausted_Throws429BeforeFirstCompletion()
    {
        var client = new ScriptedChatClient(Text("ignored"));
        var orchestrator = new AiOrchestrator(client, BuildRegistry(), new ExhaustedBudget());

        await Assert.ThrowsAsync<TooManyRequestsException>(
            () => orchestrator.RunAsync("hi", UserA, default));
        Assert.Equal(0, client.CallCount);  // never called chat client
    }

    [Fact]
    public async Task TokenBudget_DebitedAfterEachCompletion()
    {
        var args = """{"Entity":"Customer","Select":{"Mode":"list","Fields":["Mobile"]}}""";
        var client = new ScriptedChatClient(
            ToolCallWithUsage("c1", "query_customer", args, inputTokens: 100, outputTokens: 50),
            TextWithUsage("done", inputTokens: 200, outputTokens: 30));
        var budget = new RecordingBudget();

        var orchestrator = new AiOrchestrator(client, BuildRegistry(), budget);
        var result = await orchestrator.RunAsync("hi", UserA, default);

        Assert.Equal(380, result.TokensUsed);
        Assert.Equal(new[] { 150, 230 }, budget.Charges.Select(c => c.Tokens).ToArray());
        Assert.All(budget.Charges, c => Assert.Equal(UserA, c.UserId));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static QueryToolRegistry BuildRegistry()
    {
        var r = new QueryToolRegistry();
        r.Register(new StubTool("query_customer", CustomerSchema.Build()));
        r.Register(new StubTool("query_sale", SaleSchema.Build()));
        return r;
    }

    private static AiCompletionResult Text(string text, int inputTokens = 0, int outputTokens = 0) =>
        new(text, [], new AiTokenUsage(inputTokens, outputTokens), "test-model");

    private static AiCompletionResult TextWithUsage(string text, int inputTokens, int outputTokens) =>
        Text(text, inputTokens, outputTokens);

    private static AiCompletionResult ToolCall(string id, string fn, string args) =>
        new(null, [new AiToolCallRequest(id, fn, args)], AiTokenUsage.Zero, "test-model");

    private static AiCompletionResult ToolCallWithUsage(string id, string fn, string args, int inputTokens, int outputTokens) =>
        new(null, [new AiToolCallRequest(id, fn, args)], new AiTokenUsage(inputTokens, outputTokens), "test-model");

    private sealed class StubTool(string toolName, EntitySchema schema) : IQueryableTool
    {
        public string ToolName { get; } = toolName;
        public string Description => "stub";
        public EntitySchema Schema { get; } = schema;
        public int ExecuteCallCount { get; private set; }
        public Task<object> ExecuteAsync(AiQueryRequest request, CancellationToken ct)
        {
            ExecuteCallCount++;
            return Task.FromResult<object>(new { rowCount = 0, args = request });
        }
    }

    private sealed class InfiniteBudget : ITokenBudget
    {
        public bool CanProceed(Guid userId) => true;
        public void Charge(Guid userId, int tokens) { }
        public int Remaining(Guid userId) => int.MaxValue;
    }

    private sealed class ExhaustedBudget : ITokenBudget
    {
        public bool CanProceed(Guid userId) => false;
        public void Charge(Guid userId, int tokens) { }
        public int Remaining(Guid userId) => 0;
    }

    private sealed class RecordingBudget : ITokenBudget
    {
        public List<(Guid UserId, int Tokens)> Charges { get; } = [];
        public bool CanProceed(Guid userId) => true;
        public void Charge(Guid userId, int tokens) => Charges.Add((userId, tokens));
        public int Remaining(Guid userId) => int.MaxValue;
    }
}

internal sealed class ScriptedChatClient(params AiCompletionResult[] responses) : IAiChatClient
{
    private int _index;
    public int CallCount { get; private set; }

    public Task<AiCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken)
    {
        CallCount++;
        if (_index >= responses.Length)
        {
            throw new InvalidOperationException(
                $"ScriptedChatClient exhausted after {responses.Length} responses.");
        }
        return Task.FromResult(responses[_index++]);
    }

    public IAsyncEnumerable<AiStreamUpdate> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This fake only scripts the non-streaming surface.");
}
