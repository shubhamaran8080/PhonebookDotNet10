using System.Text.Json;

namespace Phonebook.Api.Tests;

/// <summary>
/// Shared JSON options for tests — matches the API's snake_case naming policy.
/// </summary>
public static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
