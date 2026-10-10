using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Phonebook.Api.Data;
using Phonebook.Api.Models.DTOs;
using CsvHelper;
using CsvHelper.Configuration;

namespace Phonebook.Api.Services;

/// <summary>
/// CSV import. Mirrors the Java CsvImportService:
/// .csv only, max 5 MB / 5000 rows, UTF-8 with BOM tolerance,
/// per-row validation, duplicate detection, single transaction.
/// </summary>
public class CsvImportService
{
    public const int MaxBytes = 5 * 1024 * 1024; // 5 MB
    public const int MaxRows = 5000;

    private static readonly string[] CanonicalColumns = { "name", "phone_number", "email", "address" };

    private readonly PhonebookDbContext _db;

    public CsvImportService(PhonebookDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Imports contacts from an uploaded CSV file.
    /// </summary>
    public async Task<CsvImportResult> ImportAsync(Stream fileStream, string filename, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filename) || !filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Only .csv files can be imported.");

        byte[] content;
        try
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, ct);
            content = ms.ToArray();
        }
        catch
        {
            throw new BadRequestException("The CSV file could not be read.");
        }

        if (content.Length > MaxBytes)
            throw new PayloadTooLargeException("The CSV file is too large (maximum is 5 MB).");

        var text = DecodeUtf8(content);
        if (string.IsNullOrWhiteSpace(text))
            throw new BadRequestException("The CSV file is empty.");

        List<Dictionary<string, string?>> validRows = [];
        List<CsvRowError> errors = [];

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                HeaderValidated = null,
                BadDataFound = null
            };

            using var reader = new StringReader(text);
            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync();
            csv.ReadHeader();

            // Map headers case-insensitively to canonical field names
            var headerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
            {
                if (header is null) continue;
                var canonical = header.Trim().ToLowerInvariant();
                if (CanonicalColumns.Contains(canonical) && !headerMap.ContainsKey(canonical))
                    headerMap[canonical] = header;
            }

            if (!headerMap.ContainsKey("name") || !headerMap.ContainsKey("phone_number"))
                throw new BadRequestException("The CSV must include 'name' and 'phone_number' columns.");

            while (await csv.ReadAsync())
            {
                if (validRows.Count + errors.Count >= MaxRows)
                    throw new PayloadTooLargeException("The CSV file has too many rows (maximum is 5000).");

                // Parser.Row counts the header row, so the first data row
                // is reported as row 2 (same numbering as the reference).
                var rowNumber = Math.Max(2, csv.Parser.Row);

                var payload = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var col in CanonicalColumns)
                {
                    payload[col] = null;
                }

                foreach (var col in headerMap)
                {
                    var value = csv.GetField(col.Value);
                    value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                    payload[col.Key] = value;
                }

                // Validate the row
                var rowErrors = ValidateRow(payload);
                if (rowErrors.Count > 0)
                {
                    errors.Add(new CsvRowError { Row = rowNumber, Errors = rowErrors });
                    continue;
                }

                validRows.Add(payload);
            }
        }
        catch (CsvHelperException)
        {
            throw new BadRequestException("The CSV file could not be parsed.");
        }

        // Duplicate detection: existing phones/emails + within-file
        var existingPhones = new HashSet<string>(
            await _db.Contacts.Select(c => c.PhoneNumber).ToListAsync(ct));
        var existingEmails = new HashSet<string>(
            await _db.Contacts.Where(c => c.Email != null).Select(c => c.Email!).ToListAsync(ct));

        var toInsert = new List<Models.Entities.Contact>();
        var imported = 0;
        var duplicates = 0;

        foreach (var payload in validRows)
        {
            var phone = payload["phone_number"]!;
            var email = payload["email"];

            if (existingPhones.Contains(phone) || (!string.IsNullOrEmpty(email) && existingEmails.Contains(email)))
            {
                duplicates++;
                continue;
            }

            toInsert.Add(new Models.Entities.Contact
            {
                Name = payload["name"]!,
                PhoneNumber = phone,
                Email = email,
                Address = payload["address"]
            });

            existingPhones.Add(phone);
            if (!string.IsNullOrEmpty(email)) existingEmails.Add(email);
            imported++;
        }

        // Single transaction: all or nothing (the in-memory provider used
        // by the tests has no transactions, so it is skipped there).
        if (_db.Database.IsRelational())
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            _db.Contacts.AddRange(toInsert);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            _db.Contacts.AddRange(toInsert);
            await _db.SaveChangesAsync(ct);
        }

        return new CsvImportResult
        {
            Total = validRows.Count + errors.Count,
            Imported = imported,
            Failed = errors.Count,
            Duplicates = duplicates,
            Errors = errors
        };
    }

    /// <summary>
    /// Validates a single row against the same rules as the web form.
    /// </summary>
    private static List<string> ValidateRow(Dictionary<string, string?> payload)
    {
        var errors = new List<string>();

        var name = payload.TryGetValue("name", out var n) ? n : null;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
            errors.Add("name: is required");

        var phone = payload.TryGetValue("phone_number", out var p) ? p : null;
        if (string.IsNullOrWhiteSpace(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+[1-9]\d{1,14}$"))
            errors.Add("phone_number: must match E.164 format");

        var email = payload.TryGetValue("email", out var e) ? e : null;
        if (!string.IsNullOrEmpty(email) && !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors.Add("email: must be a valid email address");

        return errors;
    }

    /// <summary>
    /// Strict UTF-8 decoding with optional BOM (like Python's utf-8-sig).
    /// </summary>
    private static string DecodeUtf8(byte[] content)
    {
        var offset = 0;
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            offset = 3;

        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return strictUtf8.GetString(content, offset, content.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            throw new BadRequestException("The CSV file must be UTF-8 encoded.");
        }
    }
}

/// <summary>
/// Thrown for client errors. Maps to HTTP 400.
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}

/// <summary>
/// Thrown for oversized uploads. Maps to HTTP 413.
/// </summary>
public class PayloadTooLargeException : Exception
{
    public PayloadTooLargeException(string message) : base(message) { }
}
