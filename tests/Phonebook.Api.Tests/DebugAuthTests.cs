using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Phonebook.Api.Models.DTOs;
using Xunit;

namespace Phonebook.Api.Tests;

public class DebugAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public DebugAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Debug_FullAuthFlow()
    {
        // Step 1: Create user directly in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.PhonebookDbContext>();
        var user = new Models.Entities.User
        {
            Username = "debuguser",
            Email = "debug@example.com",
            HashedPassword = Security.PasswordHasher.Hash("password123")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Step 2: Login
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "debuguser",
            Password = "password123"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestJsonOptions.Options);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrEmpty(login!.AccessToken));

        // Step 3: Call /auth/me with the token
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var meResponse = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await meResponse.Content.ReadFromJsonAsync<UserResponse>(TestJsonOptions.Options);
        Assert.Equal("debuguser", me!.Username);

        // Step 4: Call a protected endpoint
        var createResponse = await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Debug Contact",
            PhoneNumber = "+1555123456"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    }
}
