using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Phonebook.Api.Models.Entities;

namespace Phonebook.Api.Services;

/// <summary>
/// Shared contact search predicate. Case-insensitive containment on
/// name or phone number — the SQL equivalent of the Java reference's
/// LOWER(name) LIKE '%term%'.
///
/// Implemented with <c>Contains</c> instead of <c>EF.Functions.Like</c>
/// so the exact same expression runs on PostgreSQL and on the in-memory
/// provider used by the tests.
/// </summary>
public static class ContactSearch
{
    public static Expression<Func<Contact, bool>> Build(string term)
    {
        var needle = term.Trim().ToLowerInvariant();
        return contact => contact.Name.ToLower().Contains(needle)
                       || contact.PhoneNumber.ToLower().Contains(needle);
    }
}
