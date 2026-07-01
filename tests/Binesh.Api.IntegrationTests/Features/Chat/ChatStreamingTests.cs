using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Binesh.Ai.Orchestration;
using Binesh.Application.Features.Chat.Shared;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.IssueChatTicket;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Identity.Services;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Binesh.Api.IntegrationTests.Features.Chat;

/// <summary>
/// Round 13b end-to-end: ticket issuance, ticket-validated WebSocket
/// connection, streaming events, and persistence at the end. Uses
/// <c>TestServer.CreateWebSocketClient</c> so the SDK's <c>HttpMessageHandler</c>
/// is hooked end-to-end without a real network listener.
/// </summary>
public sealed class ChatStreamingTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989121211111";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        factory.AiChat.Reset();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ticket_ReturnsShortLivedJwt()
    {
        var response = await _client.PostAsync("/api/ai/chat/ticket", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IssueChatTicketResponse>();
        Assert.False(string.IsNullOrEmpty(body!.Ticket));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(body.ExpiresAt < DateTimeOffset.UtcNow.AddMinutes(2));
    }

    [Fact]
    public async Task Ticket_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync("/api/ai/chat/ticket", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WebSocket_WithoutTicket_Returns401()
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = "/api/ai/chat/ws" }.Uri;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => wsClient.ConnectAsync(uri, default));
    }

    [Fact]
    public async Task WebSocket_WithBadTicket_Returns401()
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = "/api/ai/chat/ws", Query = "ticket=not-a-jwt" }.Uri;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => wsClient.ConnectAsync(uri, default));
    }

    [Fact]
    public async Task WebSocket_HappyPath_StreamsTokens_AndPersistsAtEnd()
    {
        var conversation = await CreateConversationAsync("WS chat");
        factory.AiChat.EnqueueStream(
            new AiStreamToken("Hello"),
            new AiStreamToken(" world"),
            new AiStreamFinished("stop", new AiTokenUsage(15, 5), "fake"));

        var ticket = await GetTicketAsync();
        using var ws = await ConnectAsync(ticket);

        // Send: one client frame
        await SendJsonAsync(ws, new { conversationId = conversation.Id, message = "hi" });

        var frames = await ReceiveAllFramesAsync(ws);

        var types = frames.Select(f => f.RootElement.GetProperty("type").GetString()).ToList();
        Assert.Equal(new[] { "token", "token", "final" }, types);
        Assert.Equal("Hello", frames[0].RootElement.GetProperty("text").GetString());
        Assert.Equal(" world", frames[1].RootElement.GetProperty("text").GetString());

        var final = frames[2].RootElement;
        Assert.Equal("stop", final.GetProperty("finishReason").GetString());
        Assert.Equal(20, final.GetProperty("tokensUsed").GetInt32());

        // Verify persistence
        var detail = await _client.GetFromJsonAsync<ConversationWithMessagesDto>(
            $"/api/ai/conversations/{conversation.Id}");
        Assert.Equal(2, detail!.Messages.Count);
        Assert.Contains("Hello world", detail.Messages[1].Content);
    }

    [Fact]
    public async Task WebSocket_OtherUsersConversation_EmitsErrorFrame_AndNothingIsPersisted()
    {
        var conversation = await CreateConversationAsync("Mine");

        // Switch to a different user and use ITS ticket against the original conversation
        const string otherPhone = "+989121211222";
        await EnsureUserAsync(otherPhone, AppRoles.Admin);
        await SignInAsync(otherPhone);

        var ticket = await GetTicketAsync();
        using var ws = await ConnectAsync(ticket);
        await SendJsonAsync(ws, new { conversationId = conversation.Id, message = "sneak peek" });

        var frames = await ReceiveAllFramesAsync(ws);
        Assert.Single(frames);
        Assert.Equal("error", frames[0].RootElement.GetProperty("type").GetString());
        Assert.Equal("not_found", frames[0].RootElement.GetProperty("code").GetString());

        // Switch back; conversation still has zero messages.
        await SignInAsync(AdminPhone);
        var detail = await _client.GetFromJsonAsync<ConversationWithMessagesDto>(
            $"/api/ai/conversations/{conversation.Id}");
        Assert.Empty(detail!.Messages);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string> GetTicketAsync()
    {
        var response = await _client.PostAsync("/api/ai/chat/ticket", content: null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IssueChatTicketResponse>();
        return body!.Ticket;
    }

    private async Task<WebSocket> ConnectAsync(string ticket)
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        var uri = new UriBuilder(factory.Server.BaseAddress)
        {
            Scheme = "ws", Path = "/api/ai/chat/ws", Query = $"ticket={Uri.EscapeDataString(ticket)}",
        }.Uri;
        return await wsClient.ConnectAsync(uri, default);
    }

    private async Task<ConversationDto> CreateConversationAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/ai/conversations", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }

    private static async Task SendJsonAsync(WebSocket ws, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, default);
    }

    private static async Task<List<JsonDocument>> ReceiveAllFramesAsync(WebSocket ws)
    {
        var frames = new List<JsonDocument>();
        var buffer = new byte[4096];
        while (ws.State == WebSocketState.Open)
        {
            var sb = new StringBuilder();
            while (true)
            {
                var result = await ws.ReceiveAsync(buffer, default);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return frames;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage) { break; }
            }
            var doc = JsonDocument.Parse(sb.ToString());
            frames.Add(doc);
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type is "final" or "error") { break; }
        }
        return frames;
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
