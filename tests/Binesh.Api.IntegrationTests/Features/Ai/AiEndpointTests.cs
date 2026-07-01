using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Binesh.Application.Features.Ai.AskAi;
using Binesh.Domain.Customers;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Ai;

/// <summary>
/// Round 12c end-to-end: <c>POST /api/ai/query</c> with the orchestrator's
/// OpenAI dependency replaced by a scripted fake. Real Postgres still backs
/// the tools so each tool call runs a real SQL query through the engine.
/// </summary>
public sealed class AiEndpointTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989127777777";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        factory.AiChat.Reset();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NoToolCalls_ReturnsFinalText()
    {
        factory.AiChat.EnqueueText("سلام، چطور می‌توانم کمک کنم؟");

        var response = await _client.PostAsJsonAsync("/api/ai/query", new { message = "hi" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AskAiResponse>();
        Assert.NotNull(body);
        Assert.Equal("سلام، چطور می‌توانم کمک کنم؟", body!.AssistantText);
        Assert.Equal("stop", body.FinishReason);
        Assert.Empty(body.ToolCalls);
    }

    [Fact]
    public async Task SingleToolCall_HitsRealQueryEngine_AndAssistantSummarizes()
    {
        var args = JsonSerializer.Serialize(new
        {
            Entity = "Customer",
            Select = new { Mode = "list", Fields = new[] { "Mobile" } },
        });
        factory.AiChat.EnqueueToolCall("call_1", "query_customer", args);
        factory.AiChat.EnqueueText("There is 1 customer with mobile 0921.");

        var response = await _client.PostAsJsonAsync("/api/ai/query",
            new { message = "How many customers?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AskAiResponse>();
        Assert.NotNull(body);
        Assert.Equal("stop", body!.FinishReason);
        Assert.Single(body.ToolCalls);
        Assert.Equal("query_customer", body.ToolCalls[0].ToolName);
        Assert.Null(body.ToolCalls[0].Error);

        // The result JSON came from the REAL query engine running against the
        // seeded fixture — verify the customer's mobile appears in the
        // tool-result payload that fed the assistant.
        Assert.Contains("0921", body.ToolCalls[0].ResultJson);
        Assert.Equal("There is 1 customer with mobile 0921.", body.AssistantText);
    }

    [Fact]
    public async Task BadAiRequest_FromLLM_SurfacedAsToolError()
    {
        // The LLM sends a request that references a field that doesn't exist
        // on the Customer schema. The validator throws; the orchestrator
        // catches it and feeds the error JSON back as the tool result.
        var args = JsonSerializer.Serialize(new
        {
            Entity = "Customer",
            Select = new { Mode = "list", Fields = new[] { "DocNumber" } },  // Sale field, not Customer
        });
        factory.AiChat.EnqueueToolCall("call_2", "query_customer", args);
        factory.AiChat.EnqueueText("Sorry, I made a mistake.");

        var response = await _client.PostAsJsonAsync("/api/ai/query", new { message = "hi" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AskAiResponse>();
        Assert.Single(body!.ToolCalls);
        Assert.NotNull(body.ToolCalls[0].Error);
        Assert.Contains("DocNumber", body.ToolCalls[0].Error!);
    }

    [Fact]
    public async Task EmptyMessage_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/ai/query", new { message = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/ai/query", new { message = "hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        var person = Person.Create("Ai", "Fixture", null, null, "0921", null, null, null, null, null);
        var customer = Customer.Create(CustomerType.MoshtarianKhanegi, true, 0.8f, person);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
    }

    private async Task SignInAsync(string phone)
    {
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
        await db.Sales.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();

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
