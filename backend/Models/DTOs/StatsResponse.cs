namespace Phonebook.Api.Models.DTOs;

/// <summary>
/// Phonebook statistics for the dashboard. Mirrors the Java StatsResponse.
/// </summary>
public class StatsResponse
{
    public long TotalContacts { get; set; }
    public long RecentContacts { get; set; }
    public long WithEmail { get; set; }
    public long WithPhone { get; set; }
    public List<ContactResponse> RecentList { get; set; } = [];
}
