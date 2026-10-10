namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Per-row validation errors reported by the CSV import endpoint.
/// </summary>
public class CsvRowError
{
    public int Row { get; set; }
    public List<string> Errors { get; set; } = [];
}
