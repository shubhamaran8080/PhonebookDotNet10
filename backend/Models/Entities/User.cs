namespace Phonebook.Api.Models.Entities;

/// <summary>
/// Application user mapped to the "users" table.
/// Passwords are stored as BCrypt hashes.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string HashedPassword { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
