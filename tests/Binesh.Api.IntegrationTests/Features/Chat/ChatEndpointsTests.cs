using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Binesh.Application.Features.Chat.ListConversations;
using Binesh.Application.Features.Chat.SendChatMessage;
using Binesh.Application.Features.Chat.Shared;
using Binesh.Ai.Orchestration;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Chat;

/// <summary>
/// Round 13a end-to-end: conversation CRUD + multi-turn AI chat. The
/// orchestrator is backed by the scripted fake (no OpenAI); message
/// persistence + history replay are exercised against real Postgres.
/// </summary>
public sealed class ChatEndpointsTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminAPhone = "+989128888888";
    private const string AdminBPhone = "+989129999998";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        factory.AiChat.Reset();
        await ResetAsync();
        await EnsureUserAsync(AdminAPhone, AppRoles.Admin);
        await EnsureUserAsync(AdminBPhone, AppRoles.Admin);
        await SignInAsync(AdminAPhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartConversation_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/ai/conversations", new { title = "First chat" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.Equal("First chat", dto!.Title);
        Assert.Null(dto.ArchivedAt);
        Assert.Equal(0, dto.MessageCount);
    }

    [Fact]
    public async Task ListConversations_ScopedToCurrentUser()
    {
        await CreateConversationAsync("A1");
        await CreateConversationAsync("A2");

        // Sign in as user B and create one
        await SignInAsync(AdminBPhone);
        await CreateConversationAsync("B1");

        var page = await GetPageAsync();
        Assert.Single(page!.Items);
        Assert.Equal("B1", page.Items[0].Title);

        // Back to A
        await SignInAsync(AdminAPhone);
        page = await GetPageAsync();
        Assert.Equal(2, page!.Items.Count);

        async Task<ListConversationsResponse?> GetPageAsync()
        {
            var resp = await _client.GetAsync("/api/ai/conversations");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            return await resp.Content.ReadFromJsonAsync<ListConversationsResponse>();
        }
    }

    [Fact]
    public async Task ArchivedExcludedByDefault_IncludedWhenRequested()
    {
        var c1 = await CreateConversationAsync("Keep");
        var c2 = await CreateConversationAsync("Archive");
        await _client.DeleteAsync($"/api/ai/conversations/{c2.Id}");

        var defaultList = await (await _client.GetAsync("/api/ai/conversations"))
            .Content.ReadFromJsonAsync<ListConversationsResponse>();
        Assert.Single(defaultList!.Items);
        Assert.Equal("Keep", defaultList.Items[0].Title);

        var includeArchived = await (await _client.GetAsync("/api/ai/conversations?includeArchived=true"))
            .Content.ReadFromJsonAsync<ListConversationsResponse>();
        Assert.Equal(2, includeArchived!.Items.Count);
    }

    [Fact]
    public async Task Get_OtherUsersConversation_Returns404()
    {
        var mine = await CreateConversationAsync("Mine");
        await SignInAsync(AdminBPhone);
        var response = await _client.GetAsync($"/api/ai/conversations/{mine.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_PersistsBothTurns()
    {
        var conv = await CreateConversationAsync("Q&A");
        factory.AiChat.EnqueueText("Hello from the assistant.", new AiTokenUsage(20, 10));

        var response = await _client.PostAsJsonAsync(
            $"/api/ai/conversations/{conv.Id}/messages", new { message = "Hi there" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SendChatMessageResponse>();
        Assert.Equal("stop", body!.FinishReason);
        Assert.Equal(30, body.TokensUsed);
        Assert.Equal(1, body.UserMessage.Sequence);
        Assert.Equal(2, body.AssistantMessage.Sequence);
        Assert.Contains("Hello from the assistant.", body.AssistantMessage.Content);

        var get = await _client.GetAsync($"/api/ai/conversations/{conv.Id}");
        var detail = await get.Content.ReadFromJsonAsync<ConversationWithMessagesDto>();
        Assert.Equal(2, detail!.Messages.Count);
        Assert.Equal("User", detail.Messages[0].Role);
        Assert.Equal("Assistant", detail.Messages[1].Role);
    }

    [Fact]
    public async Task SendMessage_ReplaysHistoryToOrchestrator()
    {
        var conv = await CreateConversationAsync("Multi");
        factory.AiChat.EnqueueText("Turn 1 answer.");
        var first = await _client.PostAsJsonAsync(
            $"/api/ai/conversations/{conv.Id}/messages", new { message = "Question 1" });
        first.EnsureSuccessStatusCode();

        // Capture how many messages the second call sees.
        var lastCount = 0;
        factory.AiChat.OnInvoke = msgs => lastCount = msgs.Count;
        factory.AiChat.EnqueueText("Turn 2 answer.");

        var second = await _client.PostAsJsonAsync(
            $"/api/ai/conversations/{conv.Id}/messages", new { message = "Question 2" });
        second.EnsureSuccessStatusCode();

        // The second completion should see [system, user1, assistant1, user2] = 4 messages.
        Assert.Equal(4, lastCount);
    }

    [Fact]
    public async Task SendMessage_OnArchivedConversation_Returns409()
    {
        var conv = await CreateConversationAsync("Archived");
        await _client.DeleteAsync($"/api/ai/conversations/{conv.Id}");

        factory.AiChat.EnqueueText("unused");
        var response = await _client.PostAsJsonAsync(
            $"/api/ai/conversations/{conv.Id}/messages", new { message = "Hi" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_OtherUsersConversation_Returns404()
    {
        var mine = await CreateConversationAsync("Mine");
        await SignInAsync(AdminBPhone);
        factory.AiChat.EnqueueText("unused");
        var response = await _client.PostAsJsonAsync(
            $"/api/ai/conversations/{mine.Id}/messages", new { message = "Hi" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_AssistantPayload_EmbedsToolCallAuditLog()
    {
        var conv = await CreateConversationAsync("Tools");
        var args = JsonSerializer.Serialize(new
        {
            Entity = "Customer",
            Select = new { Mode = "list", Fields = new[] { "Mobile" } },
        });
        factory.AiChat.EnqueueToolCall("call_1", "query_customer", args);
        factory.AiChat.EnqueueText("Done.");

        var response = await _client.PostAsJsonAsync(
            $"/api/ai/conversations/{conv.Id}/messages", new { message = "Show me customers" });
        var body = await response.Content.ReadFromJsonAsync<SendChatMessageResponse>();
        Assert.Contains("query_customer", body!.AssistantMessage.Content);
        Assert.Contains("toolCalls", body.AssistantMessage.Content);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/ai/conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<ConversationDto> CreateConversationAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/ai/conversations", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }

    private async Task SignInAsync(string phone)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = phone });
        var otp = factory.Sms.GetLastOtp(phone)!;
        var verify = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = phone, otp, deviceInfo = "xunit",
        });
        verify.EnsureSuccessStatusCode();
        var tokens = (await verify.Content.ReadFromJsonAsync<VerifyOtpResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.ChatMessages.ExecuteDeleteAsync();
        await db.Conversations.ExecuteDeleteAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        foreach (var u in await userManager.Users
                     .Where(u => u.PhoneNumber != BineshApiFactory.SuperAdminPhone)
                     .ToListAsync())
        {
            await userManager.DeleteAsync(u);
        }
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        var sa = await userManager.Users.SingleAsync(u => u.PhoneNumber == BineshApiFactory.SuperAdminPhone);
        sa.LastOtpRequestedAt = null;
        await userManager.UpdateAsync(sa);
    }

    private async Task EnsureUserAsync(string phone, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.SingleOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user is null)
        {
            user = new User { UserName = phone, PhoneNumber = phone, PhoneNumberConfirmed = true };
            await userManager.CreateAsync(user);
        }
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains(role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

}
