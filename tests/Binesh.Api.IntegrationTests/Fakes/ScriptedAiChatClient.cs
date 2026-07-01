using Binesh.Ai.Orchestration;
using OpenAI.Chat;

namespace Binesh.Api.IntegrationTests.Fakes;

/// <summary>
/// Test double for <see cref="IAiChatClient"/>. Tests <see cref="Enqueue"/>
/// scripted completions; each call to <see cref="CompleteAsync"/> dequeues
/// the next one. Throws if the queue runs dry — surfaces accidental extra
/// loops as a test failure rather than a hung process.
/// </summary>
public sealed class ScriptedAiChatClient : IAiChatClient
{
    private readonly Queue<AiCompletionResult> _queue = new();
    private readonly Queue<IReadOnlyList<AiStreamUpdate>> _streamQueue = new();
    private int _callCount;

    public int CallCount => _callCount;

    /// <summary>Test hook: invoked on every CompleteAsync call with the message list the orchestrator handed us.</summary>
    public Action<IReadOnlyList<ChatMessage>>? OnInvoke { get; set; }

    public void Reset()
    {
        _queue.Clear();
        _streamQueue.Clear();
        _callCount = 0;
        OnInvoke = null;
    }

    /// <summary>
    /// Enqueue one streaming turn. The list is yielded back from
    /// <see cref="CompleteStreamingAsync"/> in order; tests can mix
    /// <see cref="AiStreamToken"/>, <see cref="AiStreamToolCall"/>, and the
    /// terminal <see cref="AiStreamFinished"/> exactly as the OpenAI SDK does.
    /// </summary>
    public void EnqueueStream(params AiStreamUpdate[] updates) =>
        _streamQueue.Enqueue(updates);

    public void EnqueueStreamingText(string text, AiTokenUsage? usage = null) =>
        _streamQueue.Enqueue(
        [
            new AiStreamToken(text),
            new AiStreamFinished("stop", usage ?? AiTokenUsage.Zero, "fake-model"),
        ]);

    public void Enqueue(AiCompletionResult result) => _queue.Enqueue(result);

    public void EnqueueText(string text) =>
        _queue.Enqueue(new AiCompletionResult(text, [], AiTokenUsage.Zero, "fake-model"));

    public void EnqueueText(string text, AiTokenUsage usage) =>
        _queue.Enqueue(new AiCompletionResult(text, [], usage, "fake-model"));

    public void EnqueueToolCall(string toolCallId, string functionName, string argumentsJson) =>
        _queue.Enqueue(new AiCompletionResult(null,
            [new AiToolCallRequest(toolCallId, functionName, argumentsJson)], AiTokenUsage.Zero, "fake-model"));

    public Task<AiCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        OnInvoke?.Invoke(messages);
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException(
                "ScriptedAiChatClient queue is empty. Did the test forget to Enqueue a response?");
        }
        return Task.FromResult(_queue.Dequeue());
    }

    public async IAsyncEnumerable<AiStreamUpdate> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ChatTool> tools,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        OnInvoke?.Invoke(messages);
        if (_streamQueue.Count == 0)
        {
            throw new InvalidOperationException(
                "ScriptedAiChatClient streaming queue is empty. Did the test forget to EnqueueStream?");
        }
        foreach (var update in _streamQueue.Dequeue())
        {
            await Task.Yield();
            yield return update;
        }
    }
}
