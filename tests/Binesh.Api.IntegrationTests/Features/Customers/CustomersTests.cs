using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Features.Customers.ListCustomers;
using Binesh.Application.Features.Customers.Shared;
using Binesh.Domain.Customers;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Customers;

public sealed class CustomersTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989121111111";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_HappyPath_Returns201WithFullCustomer()
    {
        var response = await _client.PostAsJsonAsync("/api/customers", new
        {
            type = CustomerType.MoshtarianKhanegi,
            active = true,
            paymentReliability = 0.9f,
            person = new
            {
                name = "علی",
                family = "احمدی",
                code = "C-001",
                mobile = "09121234567",
                phone = "02144556677",
                pelak = "12",
                address = "خیابان ولیعصر",
                region = new { country = "Iran", province = "Tehran", city = "Tehran" },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(dto);
        Assert.Equal(CustomerType.MoshtarianKhanegi, dto!.Type);
        Assert.True(dto.Active);
        Assert.Equal(0.9f, dto.PaymentReliability);
        Assert.Equal("علی", dto.Person.Name);
        Assert.Equal("احمدی", dto.Person.Family);
        Assert.Equal("Tehran", dto.Person.Region!.City);
    }

    [Fact]
    public async Task Create_NoRegion_OmitsRegion()
    {
        var response = await _client.PostAsJsonAsync("/api/customers", new
        {
            type = CustomerType.Personnel,
            active = true,
            paymentReliability = 1.0f,
            person = new { name = "Solo", family = "Person" },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.Null(dto!.Person.Region);
    }

    [Fact]
    public async Task Create_TwoCustomersSameRegion_DeduplicatesRegion()
    {
        var region = new { country = "Iran", province = "Isfahan", city = "Isfahan" };

        var first = await CreateCustomerAsync("First", region);
        var second = await CreateCustomerAsync("Second", region);

        // Both customers' persons point to the SAME region row (lookup-or-create).
        Assert.Equal(first.Person.Region!.Id, second.Person.Region!.Id);
    }

    [Fact]
    public async Task Create_InvalidPaymentReliability_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/customers", new
        {
            type = CustomerType.Bedehkaran,
            active = true,
            paymentReliability = 2.5f,    // out of [0, 1]
            person = new { name = "X" },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyName_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/customers", new
        {
            type = CustomerType.Bedehkaran,
            active = true,
            paymentReliability = 0.5f,
            person = new { name = "" },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsCustomer()
    {
        var created = await CreateCustomerAsync("Lookup", null);

        var response = await _client.GetAsync($"/api/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.Equal(created.Id, dto!.Id);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/customers/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_FilterByType_OnlyMatchingReturned()
    {
        await CreateCustomerAsync("A", null, type: CustomerType.Personnel);
        await CreateCustomerAsync("B", null, type: CustomerType.Bazaryab);
        await CreateCustomerAsync("C", null, type: CustomerType.Personnel);

        var response = await _client.GetAsync($"/api/customers?type={CustomerType.Personnel}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<ListCustomersResponse>();
        Assert.NotNull(page);
        Assert.All(page!.Items, c => Assert.Equal(CustomerType.Personnel, c.Type));
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task List_SearchByName_MatchesPartial()
    {
        await CreateCustomerAsync("Alice", null);
        await CreateCustomerAsync("Bob", null);

        var response = await _client.GetAsync("/api/customers?search=lic");
        var page = await response.Content.ReadFromJsonAsync<ListCustomersResponse>();
        Assert.Single(page!.Items);
        Assert.Equal("Alice", page.Items[0].Person.Name);
    }

    [Fact]
    public async Task List_NoQueryParams_DefaultsToPage1Size20()
    {
        var response = await _client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ListCustomersResponse>();
        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task Update_PartialPatch_UpdatesOnlySetFields()
    {
        var created = await CreateCustomerAsync("Original", null, paymentReliability: 0.3f);

        var response = await _client.PutAsJsonAsync($"/api/customers/{created.Id}", new
        {
            paymentReliability = 0.8f,
            // type, active, person all unset → unchanged
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.Equal(0.8f, updated!.PaymentReliability);
        Assert.Equal("Original", updated.Person.Name);  // unchanged
        Assert.Equal(created.Type, updated.Type);       // unchanged
    }

    [Fact]
    public async Task Update_PersonFields_PatchesPerson()
    {
        var created = await CreateCustomerAsync("Original", null);

        var response = await _client.PutAsJsonAsync($"/api/customers/{created.Id}", new
        {
            person = new
            {
                name = "Renamed",
                mobile = "09120000000",
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.Equal("Renamed", updated!.Person.Name);
        Assert.Equal("09120000000", updated.Person.Mobile);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}", new
        {
            paymentReliability = 0.5f,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingCustomer_Returns204AndCascadesPerson()
    {
        var created = await CreateCustomerAsync("ToDelete", null);

        var response = await _client.DeleteAsync($"/api/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.Null(await db.Customers.SingleOrDefaultAsync(c => c.Id == created.Id));
        Assert.Null(await db.Persons.SingleOrDefaultAsync(p => p.Id == created.Person.Id));  // CASCADE
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/customers/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<CustomerDto> CreateCustomerAsync(
        string name,
        object? region,
        CustomerType type = CustomerType.MoshtarianKhanegi,
        float paymentReliability = 0.5f)
    {
        var body = new
        {
            type,
            active = true,
            paymentReliability,
            person = new
            {
                name,
                region,
            },
        };
        var response = await _client.PostAsJsonAsync("/api/customers", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    private async Task SignInAsync(string phone)
    {
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = phone });
        var otp = factory.Sms.GetLastOtp(phone)!;
        var verify = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = phone,
            otp,
            deviceInfo = "xunit",
        });
        verify.EnsureSuccessStatusCode();
        var tokens = (await verify.Content.ReadFromJsonAsync<VerifyOtpResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.Customers.ExecuteDeleteAsync();
        // Persons cascade with Customers; clean any orphans defensively.
        await db.Persons.ExecuteDeleteAsync();
        await db.Regions.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastOtpRequestedAt, (DateTimeOffset?)null));
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
