using System.ComponentModel.DataAnnotations;

namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Payload for creating a contact. Mirrors the Java ContactCreateRequest.
/// </summary>
public class ContactCreateRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\+[1-9]\d{1,14}$", ErrorMessage = "Phone number must be in E.164 format, e.g. +1234567890")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(1000)]
    public string? Address { get; set; }
}
