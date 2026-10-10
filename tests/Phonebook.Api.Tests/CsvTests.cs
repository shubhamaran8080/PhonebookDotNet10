using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Phonebook.Api.Models.DTOs;
using Xunit;

namespace Phonebook.Api.Tests;

public class CsvTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public CsvTests(CustomWebApplicationFactory factory)
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
            Username = "csvuser",
            Email = "csv@example.com",
            HashedPassword = Security.PasswordHasher.Hash("password123")
        };
        db.Users.Add(user);
        db.SaveChanges();

        var loginResponse = _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Username = "csvuser",
            Password = "password123"
        }, TestJsonOptions.Options).GetAwaiter().GetResult();
        var login = loginResponse.Content.ReadFromJsonAsync<LoginResponse>(TestJsonOptions.Options).GetAwaiter().GetResult();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    [Fact]
    public async Task ImportCsv_ValidFile_ReturnsSuccess()
    {
        var csv = "name,phone_number,email,address\nJohn Doe,+1234567890,john@example.com,123 Main St\nJane Smith,+1987654321,jane@example.com,456 Oak Ave";
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(csv), "file", "contacts.csv");

        var response = await _client.PostAsync("/contacts/import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CsvImportResult>(TestJsonOptions.Options);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Total);
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task ImportCsv_WithDuplicates_SkipsThem()
    {
        var csv1 = "name,phone_number\nFirst,+1234567890";
        var content1 = new MultipartFormDataContent();
        content1.Add(new StringContent(csv1), "file", "first.csv");
        await _client.PostAsync("/contacts/import", content1);

        var csv2 = "name,phone_number\nSecond,+1234567890";
        var content2 = new MultipartFormDataContent();
        content2.Add(new StringContent(csv2), "file", "second.csv");

        var response = await _client.PostAsync("/contacts/import", content2);
        var result = await response.Content.ReadFromJsonAsync<CsvImportResult>(TestJsonOptions.Options);
        Assert.Equal(1, result!.Duplicates);
        Assert.Equal(0, result.Imported);
    }

    [Fact]
    public async Task ImportCsv_WithInvalidRows_ReturnsErrors()
    {
        var csv = "name,phone_number\nValid,+1234567890\nInvalid,not-a-phone";
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(csv), "file", "test.csv");

        var response = await _client.PostAsync("/contacts/import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CsvImportResult>(TestJsonOptions.Options);
        Assert.Equal(1, result!.Failed);
        Assert.Single(result.Errors);
        Assert.Equal(3, result.Errors[0].Row);
    }

    [Fact]
    public async Task ImportCsv_InvalidExtension_Returns400()
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("test"), "file", "contacts.txt");

        var response = await _client.PostAsync("/contacts/import", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExportCsv_ReturnsCsvFile()
    {
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Export Test",
            PhoneNumber = "+1555123456"
        }, TestJsonOptions.Options);

        var response = await _client.GetAsync("/contacts/export");
        response.EnsureSuccessStatusCode();

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name,phone_number,email,address", body);
        Assert.Contains("Export Test", body);
    }

    [Fact]
    public async Task ExportCsv_WithSearchFilter_ExportsOnlyMatches()
    {
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Alice Special",
            PhoneNumber = "+1555000001"
        }, TestJsonOptions.Options);
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Bob Regular",
            PhoneNumber = "+1555000002"
        }, TestJsonOptions.Options);

        var response = await _client.GetAsync("/contacts/export?q=alice");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Alice Special", body);
        Assert.DoesNotContain("Bob Regular", body);
    }

    [Fact]
    public async Task ExportCsv_FormulaInjection_ProtectsPhoneNumbers()
    {
        await _client.PostAsJsonAsync("/contacts", new ContactCreateRequest
        {
            Name = "Test",
            PhoneNumber = "+1555123456"
        }, TestJsonOptions.Options);

        var response = await _client.GetAsync("/contacts/export");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("'+1555123456", body);
    }

    [Fact]
    public async Task ExportCsv_EmptyResult_ReturnsHeadersOnly()
    {
        var response = await _client.GetAsync("/contacts/export?q=nonexistent");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("name,phone_number,email,address", body);
    }
}
