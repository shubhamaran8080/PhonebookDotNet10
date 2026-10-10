using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using CsvHelper.Configuration;
using Phonebook.Api.Data;

namespace Phonebook.Api.Services;

/// <summary>
/// CSV export. Mirrors the Java CsvExportService:
/// all matching contacts (not just current page), LF line endings,
/// formula-injection protection for values starting with =, +, -, @.
/// </summary>
public class CsvExportService
{
    private readonly PhonebookDbContext _db;

    public CsvExportService(PhonebookDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Builds the CSV document containing every contact the search matches.
    /// </summary>
    public async Task<byte[]> BuildAsync(string? search, CancellationToken ct = default)
    {
        var query = _db.Contacts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(ContactSearch.Build(search));
        }

        var contacts = await query.OrderBy(c => c.Id).ToListAsync(ct);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = "\n",
            // The reference backend does not emit a BOM.
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        // The document is built in memory and returned as bytes. Writing
        // directly into the Kestrel response stream produced truncated
        // transfers, while a buffered response also lets the framework
        // send a correct Content-Length header.
        var buffer = new MemoryStream();
        await using (var writer = new StreamWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            var csv = new CsvWriter(writer, config);

            csv.WriteField("name");
            csv.WriteField("phone_number");
            csv.WriteField("email");
            csv.WriteField("address");
            csv.NextRecord();

            foreach (var contact in contacts)
            {
                csv.WriteField(Sanitize(contact.Name));
                csv.WriteField(Sanitize(contact.PhoneNumber));
                csv.WriteField(Sanitize(contact.Email));
                csv.WriteField(Sanitize(contact.Address));
                csv.NextRecord();
            }

            await writer.FlushAsync(ct);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Prefixes values that spreadsheet apps could interpret as formulas
    /// (every phone number, since they start with +) with a single quote.
    /// </summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if ("=+-@".IndexOf(value[0]) >= 0)
            return "'" + value;
        return value;
    }
}
