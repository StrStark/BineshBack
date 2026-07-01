using Binesh.Ai.Orchestration;
using Binesh.Application.Features.Ai.AskAi;
using MediatR;

namespace Binesh.Ai.Application;

/// <summary>
/// MediatR handler for <see cref="AskAiCommand"/>. Lives in the Ai assembly
/// because <see cref="AiOrchestrator"/> is here; Binesh.Application picks it
/// up via the extra-assembly argument to <c>AddApplication</c>, same way
/// Identity slices are registered.
/// </summary>
public sealed class AskAiHandler(AiOrchestrator orchestrator) : IRequestHandler<AskAiCommand, AskAiResponse>
{
    public async Task<AskAiResponse> Handle(AskAiCommand request, CancellationToken cancellationToken)
    {
        var run = await orchestrator.RunAsync(request.Message, request.UserId, cancellationToken);
        return new AskAiResponse(
            run.AssistantText,
            run.FinishReason,
            run.TokensUsed,
            run.ToolCalls.Select(c => new AskAiToolCall(c.ToolName, c.ArgumentsJson, c.ResultJson, c.Error)).ToList());
    }
}
