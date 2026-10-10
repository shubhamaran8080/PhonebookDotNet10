using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Phonebook.Api.Models.DTOs;
using Xunit;

namespace Phonebook.Api.Tests;

public class ContactTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ContactTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        SetupAuth();
    }

    private void SetupAuth()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.PhonebookDbContext>();
        var user = new Models.Entities.User
        {
            Username = "testuser",
            Email = "test@example.com",
            HashedPassword = Security.PasswordHasher.Hash("password123")
        };
        db.Users.Add(user);
        db.SaveChanges();

        var loginResponse = _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "testuser",
            Password = "password123"
        }, TestJsonOptions.Options).GetAwaiter().GetResult();
        var login = loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestJsonOptions.Options).GetAwaiter().GetResult();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    [Fact]
    public async Task CreateContact_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "John Doe",
            PhoneNumber = "+1234567890",
            Email = "john@example.com",
            Address = "123 Main St"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var contact = await response.Content.ReadFromJsonAsync<ContactResponse>(TestJsonOptions.Options);
        Assert.NotNull(contact);
        Assert.True(contact!.Id > 0);
        Assert.Equal("John Doe", contact.Name);
    }

    [Fact]
    public async Task CreateContact_WithInvalidPhone_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "John",
            PhoneNumber = "not-a-phone"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateContact_WithDuplicatePhone_Returns409()
    {
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "First",
            PhoneNumber = "+1234567890"
        }, TestJsonOptions.Options);

        var response = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Second",
            PhoneNumber = "+1234567890"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetContact_ById_ReturnsContact()
    {
        var createResponse = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Jane",
            PhoneNumber = "+1987654321"
        }, TestJsonOptions.Options);
        var created = await createResponse.Content.ReadFromJsonAsync<ContactResponse>(TestJsonOptions.Options);

        var response = await _client.GetAsync($"/contacts/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contact = await response.Content.ReadFromJsonAsync<ContactResponse>(TestJsonOptions.Options);
        Assert.Equal("Jane", contact!.Name);
    }

    [Fact]
    public async Task GetContact_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/contacts/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContact_PartialUpdate_OnlyChangesProvided()
    {
        var createResponse = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Original",
            PhoneNumber = "+1111111111",
            Email = "orig@example.com",
            Address = "Old Address"
        }, TestJsonOptions.Options);
        var created = await createResponse.Content.ReadFromJsonAsync<ContactResponse>(TestJsonOptions.Options);

        var updateResponse = await _client.PutAsJsonAsync($"/contacts/{created!.Id}", new ContactUpdateRequest
        {
            Name = "Updated",
            NameSet = true
        }, TestJsonOptions.Options);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ContactResponse>(TestJsonOptions.Options);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal("+1111111111", updated.PhoneNumber);
        Assert.Equal("orig@example.com", updated.Email);
    }

    [Fact]
    public async Task DeleteContact_Returns204()
    {
        var createResponse = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "ToDelete",
            PhoneNumber = "+1222222222"
        }, TestJsonOptions.Options);
        var created = await createResponse.Content.ReadFromJsonAsync<ContactResponse>(TestJsonOptions.Options);

        var deleteResponse = await _client.DeleteAsync($"/contacts/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/contacts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task ListContacts_WithPagination_ReturnsHeaders()
    {
        for (int i = 0; i < 15; i++)
        {
            await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
            {
                Name = $"Contact {i}",
                PhoneNumber = $"+1555000{i:D4}"
            }, TestJsonOptions.Options);
        }

        var response = await _client.GetAsync("/contacts?page=2&page_size=10");
        response.EnsureSuccessStatusCode();

        Assert.Equal("15", response.Headers.GetValues("X-Total-Count").First());
        Assert.Equal("2", response.Headers.GetValues("X-Page").First());
        Assert.Equal("10", response.Headers.GetValues("X-Page-Size").First());
    }

    [Fact]
    public async Task SearchContacts_ByName_ReturnsMatches()
    {
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Alice Wonderland",
            PhoneNumber = "+1666000001"
        }, TestJsonOptions.Options);
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Bob Builder",
            PhoneNumber = "+1666000002"
        }, TestJsonOptions.Options);

        var response = await _client.GetAsync("/contacts?q=alice");
        response.EnsureSuccessStatusCode();

        var contacts = await response.Content.ReadFromJsonAsync<List<ContactResponse>>(TestJsonOptions.Options);
        Assert.Single(contacts!);
        Assert.Equal("Alice Wonderland", contacts[0].Name);
    }
}
