namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Error body used for every API error: {"detail": "message"}.
/// </summary>
public class ErrorResponse
{
    public string Detail { get; set; } = string.Empty;

    public ErrorResponse() { }

    public ErrorResponse(string detail)
    {
        Detail = detail;
    }
}
