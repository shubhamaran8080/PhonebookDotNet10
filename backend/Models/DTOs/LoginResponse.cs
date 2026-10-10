namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Response of the login endpoint. Mirrors the Java LoginResponse.
/// </summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "bearer";
    public UserResponse User { get; set; } = new();
}
