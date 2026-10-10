using System.ComponentModel.DataAnnotations;

namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Login payload. The "username" field accepts a username or an email address.
/// </summary>
public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
