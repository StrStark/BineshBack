using System.Text.Json;
using Binesh.Ai.Orchestration;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Ai.Tools;
using OpenAI.Chat;

namespace Binesh.Ai.IntegrationTests.Orchestration;

public sealed class AiOrchestratorStreamingTests
{
    private static readonly Guid UserA = Guid.NewGuid();

    [Fact]
    public async Task TextOnly_Stream_YieldsTokensThenFinal()
    {
        var client = new StreamingScriptedClient(
        [
            new AiStreamToken("Hello"),
            new AiStreamToken(", "),
            new AiStreamToken("world."),
            new AiStreamFinished("stop", new AiTokenUsage(20, 10), "fake"),
        ]);

        var orchestrator = new AiOrchestrator(client, BuildRegistry(), new InfiniteBudget());
        var events = new List<OrchestratorStreamEvent>();
        await foreach (var ev in orchestrator.RunStreamingAsync("hi", UserA, [], default))
        {
            events.Add(ev);
        }

        Assert.Equal(4, events.Count);
        Assert.Collection(events.Take(3),
            e => Assert.Equal("Hello", ((OrchestratorStreamToken)e).Text),
            e => Assert.Equal(", ", ((OrchestratorStreamToken)e).Text),
            e => Assert.Equal("world.", ((OrchestratorStreamToken)e).Text));

        var final = (OrchestratorStreamFinal)events[3];
        Assert.Equal("Hello, world.", final.AssistantText);
        Assert.Equal("stop", final.FinishReason);
        Assert.Equal(30, final.TokensUsed);
        Assert.Empty(final.ToolCalls);
    }

    [Fact]
    public async Task ToolCall_Stream_DispatchesAndCompletes_BeforeSecondTurnText()
    {
        var args = JsonSerializer.Serialize(new
        {
            Entity = "Customer",
            Select = new { Mode = "list", Fields = new[] { "Mobile" } },
        });

        var client = new StreamingScriptedClient(
            // Turn 1: model emits a tool call (no text)
            [
                new AiStreamToolCall("call_1", "query_customer", args),
                new AiStreamFinished("tool_calls", new AiTokenUsage(40, 20), "fake"),
            ],
            // Turn 2: model emits final answer text
            [
                new AiStreamToken("Done."),
                new AiStreamFinished("stop", new AiTokenUsage(10, 5), "fake"),
            ]);

        var registry = BuildRegistry();
        var orchestrator = new AiOrchestrator(client, registry, new InfiniteBudget());

        var events = new List<OrchestratorStreamEvent>();
        await foreach (var ev in orchestrator.RunStreamingAsync("Show me customers", UserA, [], default))
        {
            events.Add(ev);
        }

        Assert.IsType<OrchestratorStreamToolCallDispatched>(events[0]);
        Assert.Equal("query_customer", ((OrchestratorStreamToolCallDispatched)events[0]).ToolName);
        Assert.IsType<OrchestratorStreamToolCallCompleted>(events[1]);
        Assert.IsType<OrchestratorStreamToken>(events[2]);
        Assert.Equal("Done.", ((OrchestratorStreamToken)events[2]).Text);
        var final = (OrchestratorStreamFinal)events[^1];
        Assert.Equal("Done.", final.AssistantText);
        Assert.Single(final.ToolCalls);
        Assert.Equal(75, final.TokensUsed);  // 60 turn1 + 15 turn2
    }

    private static QueryToolRegistry BuildRegistry()
    {
        var r = new QueryToolRegistry();
        r.Register(new StubTool("query_customer", CustomerSchema.Build()));
        return r;
    }

    private sealed class StubTool(string toolName, EntitySchema schema) : IQueryableTool
    {
        public string ToolName { get; } = toolName;
        public string Description => "stub";
        public EntitySchema Schema { get; } = schema;
        public Task<object> ExecuteAsync(AiQueryRequest request, CancellationToken ct) =>
            Task.FromResult<object>(new { rowCount = 0 });
    }

    private sealed class InfiniteBudget : ITokenBudget
    {
        public bool CanProceed(Guid userId) => true;
        public void Charge(Guid userId, int tokens) { }
        public int Remaining(Guid userId) => int.MaxValue;
    }

    private sealed class StreamingScriptedClient(params AiStreamUpdate[][] turns) : IAiChatClient
    {
        private int _turn;

        public Task<AiCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages, IReadOnlyList<ChatTool> tools, CancellationToken ct) =>
            throw new NotSupportedException("Streaming-only fake.");

        public async IAsyncEnumerable<AiStreamUpdate> CompleteStreamingAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ChatTool> tools,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_turn >= turns.Length)
            {
                throw new InvalidOperationException($"Streaming script exhausted after {turns.Length} turns.");
            }
            foreach (var u in turns[_turn++])
            {
                await Task.Yield();
                yield return u;
            }
        }
    }
}
