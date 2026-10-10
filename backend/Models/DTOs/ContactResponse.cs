using Phonebook.Api.Models.Entities;

namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Contact as returned by the API. Mirrors the Java ContactResponse.
/// </summary>
public class ContactResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static ContactResponse FromEntity(Contact contact) => new()
    {
        Id = contact.Id,
        Name = contact.Name,
        PhoneNumber = contact.PhoneNumber,
        Email = contact.Email,
        Address = contact.Address,
        CreatedAt = contact.CreatedAt
    };
}
