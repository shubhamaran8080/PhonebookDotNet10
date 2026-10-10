namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Summary returned by the CSV import endpoint. Mirrors the Java CsvImportResult.
/// </summary>
public class CsvImportResult
{
    public int Total { get; set; }
    public int Imported { get; set; }
    public int Failed { get; set; }
    public int Duplicates { get; set; }
    public List<CsvRowError> Errors { get; set; } = [];
}
