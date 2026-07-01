namespace Binesh.Ai.Orchestration;

/// <summary>
/// Per-user token budget enforcer. Called by <see cref="AiOrchestrator"/> on
/// every chat-completion turn so a runaway tool-call loop can't drain a
/// user's daily allotment in a single conversation.
/// </summary>
public interface ITokenBudget
{
    /// <summary>
    /// Returns true if <paramref name="userId"/> has any budget remaining
    /// for the current rolling window. Cheap — does not record a charge.
    /// </summary>
    bool CanProceed(Guid userId);

    /// <summary>Debits <paramref name="tokens"/> from the user's remaining budget.</summary>
    void Charge(Guid userId, int tokens);

    /// <summary>Inspects remaining budget for diagnostics. Negative means overdrawn (latest call exceeded the cap).</summary>
    int Remaining(Guid userId);
}
