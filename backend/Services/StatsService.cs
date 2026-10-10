using Microsoft.EntityFrameworkCore;
using Phonebook.Api.Data;
using Phonebook.Api.Models.DTOs;

namespace Phonebook.Api.Services;

/// <summary>
/// Dashboard statistics. Mirrors the Java StatsService.
/// </summary>
public class StatsService
{
    private readonly PhonebookDbContext _db;

    public StatsService(PhonebookDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns total contacts, contacts added in the last 7 days,
    /// contacts with email, contacts with phone, and the 5 most recent.
    /// </summary>
    public async Task<StatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        var total = await _db.Contacts.CountAsync(ct);
        var recent = await _db.Contacts.CountAsync(c => c.CreatedAt >= since, ct);
        var withEmail = await _db.Contacts.CountAsync(c => c.Email != null, ct);
        var withPhone = await _db.Contacts.CountAsync(c => c.PhoneNumber != null, ct);

        var recentList = await _db.Contacts
            .AsNoTracking()
            .Where(c => c.CreatedAt >= since)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(5)
            .ToListAsync(ct);

        return new StatsResponse
        {
            TotalContacts = total,
            RecentContacts = recent,
            WithEmail = withEmail,
            WithPhone = withPhone,
            RecentList = recentList.Select(ContactResponse.FromEntity).ToList()
        };
    }
}
