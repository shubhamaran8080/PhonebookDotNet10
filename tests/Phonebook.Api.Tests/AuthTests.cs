using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Phonebook.Api.Data;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Security;
using Xunit;

namespace Phonebook.Api.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task CreateUser(string username, string password, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhonebookDbContext>();
        var user = new Models.Entities.User
        {
            Username = username,
            Email = email,
            HashedPassword = PasswordHasher.Hash(password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestJsonOptions.Options);
        Assert.Equal("ok", body!["status"]);
    }

    [Fact]
    public async Task Login_WithValidUsername_ReturnsToken()
    {
        await CreateUser("alice", "secret123", "alice@example.com");

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "alice",
            Password = "secret123"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestJsonOptions.Options);
        Assert.NotNull(body);
        Assert.Equal("bearer", body!.TokenType);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.Equal("alice", body.User.Username);
    }

    [Fact]
    public async Task Login_WithValidEmail_ReturnsToken()
    {
        await CreateUser("alice", "secret123", "alice@example.com");

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "alice@example.com",
            Password = "secret123"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await CreateUser("alice", "secret123", "alice@example.com");

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "alice",
            Password = "wrong"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJsonOptions.Options);
        Assert.Equal("Incorrect username/email or password", body!.Detail);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "nobody",
            Password = "x"
        }, TestJsonOptions.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUser()
    {
        await CreateUser("alice", "secret123", "alice@example.com");

        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "alice",
            Password = "secret123"
        }, TestJsonOptions.Options);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestJsonOptions.Options);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(TestJsonOptions.Options);
        Assert.Equal("alice", user!.Username);
        Assert.Equal("alice@example.com", user.Email);
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        var response = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
