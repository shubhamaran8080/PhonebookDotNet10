namespace Phonebook.Api.Models.Entities;

/// <summary>
/// A phonebook entry mapped to the "contacts" table.
/// Contacts are global and shared among all authenticated users
/// (no per-user ownership, matching the Java reference).
/// </summary>
public class Contact
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
