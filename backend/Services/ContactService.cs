using Microsoft.EntityFrameworkCore;
using Phonebook.Api.Data;
using Phonebook.Api.Models.DTOs;
using Phonebook.Api.Models.Entities;

namespace Phonebook.Api.Services;

/// <summary>
/// Contact CRUD operations. Mirrors the Java ContactService.
/// </summary>
public class ContactService
{
    private readonly PhonebookDbContext _db;

    public ContactService(PhonebookDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets a contact by ID or throws NotFoundException.
    /// </summary>
    public async Task<Contact> GetContactAsync(int id, CancellationToken ct = default)
    {
        var contact = await _db.Contacts.FindAsync(new object[] { id }, ct);
        if (contact is null)
            throw new NotFoundException("Contact not found");
        return contact;
    }

    /// <summary>
    /// Creates a new contact. Throws ConflictException on duplicate phone/email.
    /// </summary>
    public async Task<ContactResponse> CreateAsync(ContactCreateRequest request, CancellationToken ct = default)
    {
        var contact = new Contact
        {
            Name = request.Name.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim()
        };

        await EnsureUniqueAsync(contact.Id, contact.PhoneNumber, contact.Email, ct);

        _db.Contacts.Add(contact);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("A contact with this phone number or email already exists.");
        }

        return ContactResponse.FromEntity(contact);
    }

    /// <summary>
    /// Partial update: only fields explicitly provided are changed.
    /// </summary>
    public async Task<ContactResponse> UpdateAsync(int id, ContactUpdateRequest request, CancellationToken ct = default)
    {
        var contact = await GetContactAsync(id, ct);

        if (request.NameSet)
            contact.Name = request.Name?.Trim() ?? string.Empty;
        if (request.PhoneNumberSet)
            contact.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
        if (request.EmailSet)
            contact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.AddressSet)
            contact.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        await EnsureUniqueAsync(contact.Id, contact.PhoneNumber, contact.Email, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("A contact with this phone number or email already exists.");
        }

        return ContactResponse.FromEntity(contact);
    }

    /// <summary>
    /// Deletes a contact by ID.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var contact = await GetContactAsync(id, ct);
        _db.Contacts.Remove(contact);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Searches contacts by name or phone number (case-insensitive), ordered by ID.
    /// </summary>
    public async Task<(List<Contact> Items, long Total)> SearchAsync(
        string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Contacts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(ContactSearch.Build(q));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Reports duplicates with the same 409 the database unique indexes
    /// would produce. The explicit check also covers providers that do
    /// not enforce the unique indexes (the in-memory test provider).
    /// </summary>
    private async Task EnsureUniqueAsync(int id, string phoneNumber, string? email, CancellationToken ct)
    {
        var duplicatePhone = await _db.Contacts
            .AnyAsync(c => c.Id != id && c.PhoneNumber == phoneNumber, ct);

        if (duplicatePhone)
            throw new ConflictException("A contact with this phone number or email already exists.");

        if (string.IsNullOrWhiteSpace(email))
            return;

        var duplicateEmail = await _db.Contacts
            .AnyAsync(c => c.Id != id && c.Email == email, ct);

        if (duplicateEmail)
            throw new ConflictException("A contact with this phone number or email already exists.");
    }
}

/// <summary>
/// Thrown when a resource is not found. Maps to HTTP 404.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a duplicate record is detected. Maps to HTTP 409.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
