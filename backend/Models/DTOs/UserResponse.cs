using Phonebook.Api.Models.Entities;

namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// User info returned by the API. Never includes the password hash.
/// </summary>
public class UserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static UserResponse FromEntity(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        CreatedAt = user.CreatedAt
    };
}
