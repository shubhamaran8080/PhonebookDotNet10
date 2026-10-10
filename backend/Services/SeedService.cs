using Phonebook.Api.Data;
using Phonebook.Api.Models.Entities;
using Phonebook.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Phonebook.Api.Services;

/// <summary>
/// Controlled seed utility. Disabled by default, idempotent,
/// never overwrites existing users or contacts. Mirrors the Java SeedRunner.
/// </summary>
public class SeedService
{
    private readonly PhonebookDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SeedService> _logger;

    public SeedService(PhonebookDbContext db, IConfiguration config, ILogger<SeedService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Runs the seed operation if explicitly enabled via configuration.
    /// Safe: only runs when Seed:CreateUser is true or Seed:Count > 0.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        var createUser = _config.GetValue("Seed:CreateUser", false);
        var count = _config.GetValue("Seed:Count", 0);

        if (!createUser && count <= 0)
            return;

        if (createUser)
            await CreateUserAsync(ct);

        if (count > 0)
            await GenerateContactsAsync(count, ct);
    }

    private async Task CreateUserAsync(CancellationToken ct)
    {
        var username = _config["Seed:Username"];
        if (string.IsNullOrWhiteSpace(username))
            return; // Username required

        username = username.Trim();

        // Idempotent: never overwrite existing user
        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
            return;

        var password = _config["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password))
            return; // Password required

        var email = _config["Seed:Email"];
        if (!string.IsNullOrWhiteSpace(email))
            email = email.Trim();

        var user = new User
        {
            Username = username,
            Email = email,
            HashedPassword = PasswordHasher.Hash(password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Inserts exactly <paramref name="count"/> brand new contacts.
    ///
    /// - Existing rows are only READ, never updated or deleted.
    /// - Phone numbers and emails are unique against the database AND
    ///   against the rest of the generated batch.
    /// - When a candidate collides with an existing record it is simply
    ///   discarded and a replacement is generated, until enough valid
    ///   contacts have been produced.
    /// </summary>
    private async Task GenerateContactsAsync(int count, CancellationToken ct)
    {
        var before = await _db.Contacts.CountAsync(ct);
        _logger.LogInformation(
            "Seed: generating {Count} dummy contacts (existing: {Before})", count, before);

        // --- Unique keys already present in the database ---
        var usedPhones = new HashSet<string>(
            await _db.Contacts.Select(c => c.PhoneNumber).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var usedEmails = new HashSet<string>(
            await _db.Contacts.Where(c => c.Email != null).Select(c => c.Email!).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var random = Random.Shared;
        var batch = new List<Contact>();
        var created = 0;
        // Each record gets a generous number of retries; the random space is
        // large enough that this is never hit in practice.
        var attempts = 0;
        const int batchSize = 500;
        const int maxAttemptsPerRecord = 100;

        while (created < count)
        {
            if (attempts++ > count * maxAttemptsPerRecord)
                throw new InvalidOperationException(
                    $"Unable to generate {count} unique contacts after {attempts} attempts.");

            var phone = RandomPhone(random);
            if (!usedPhones.Add(phone))
                continue;

            var email = RandomEmail(random);
            if (!usedEmails.Add(email))
            {
                // Release the phone so it can be picked again by another row.
                usedPhones.Remove(phone);
                continue;
            }

            batch.Add(new Contact
            {
                Name = RandomName(random),
                PhoneNumber = phone,
                Email = email,
                Address = RandomAddress(random),
                CreatedAt = DateTimeOffset.UtcNow
            });
            created++;

            if (batch.Count < batchSize)
                continue;

            _db.Contacts.AddRange(batch);
            await _db.SaveChangesAsync(ct);
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            _db.Contacts.AddRange(batch);
            await _db.SaveChangesAsync(ct);
        }

        var after = await _db.Contacts.CountAsync(ct);
        _logger.LogInformation(
            "Seed: inserted {Inserted} contacts (contacts now: {After})", created, after);
    }

    // ------------------------------------------------------------------
    // Realistic dummy data
    // ------------------------------------------------------------------

    private static string RandomName(Random random)
    {
        var first = FirstNames[random.Next(FirstNames.Length)];
        var last = LastNames[random.Next(LastNames.Length)];
        return $"{first} {last}";
    }

    /// <summary>
    /// NANP-style number: +1, a 2xx-9xx area code, a 2xx-9xx exchange code
    /// and a 4-digit subscriber number — always valid E.164 for this app.
    /// </summary>
    private static string RandomPhone(Random random) =>
        $"+1{random.Next(200, 1000):D3}{random.Next(200, 1000):D3}{random.Next(0, 10000):D4}";

    /// <summary>
    /// firstname.lastname##@&lt;reserved-example-domain&gt;. The example domains
    /// are reserved (RFC 2606) so no real mailbox can be addressed.
    /// </summary>
    private static string RandomEmail(Random random)
    {
        var first = FirstNames[random.Next(FirstNames.Length)].ToLowerInvariant();
        var last = LastNames[random.Next(LastNames.Length)].ToLowerInvariant();
        var domain = EmailDomains[random.Next(EmailDomains.Length)];
        return $"{first}.{last}{random.Next(1, 1000):D3}@{domain}";
    }

    private static string RandomAddress(Random random)
    {
        var number = random.Next(1, 9999);
        var street = StreetNames[random.Next(StreetNames.Length)];
        var city = Cities[random.Next(Cities.Length)];

        // Roughly two thirds have an apartment/unit.
        var unit = random.Next(3) switch
        {
            0 => $", Apt {random.Next(1, 99)}",
            1 => $", Unit {random.Next(1, 400)}",
            _ => string.Empty
        };

        return $"{number} {street}{unit}, {city}";
    }

    private static readonly string[] FirstNames =
    [
        "Aarav", "Abigail", "Aiden", "Aisha", "Alejandro", "Alicia", "Amara", "Amelia",
        "Ananya", "Andre", "Anthony", "Aria", "Arjun", "Asha", "Ava", "Beatriz",
        "Bilal", "Brandon", "Camille", "Carlos", "Cecilia", "Chloe", "Clara", "Daniel",
        "Darius", "David", "Diego", "Elena", "Elias", "Emeka", "Emma", "Ethan",
        "Farrah", "Fatima", "Felix", "Gabriel", "Gabriela", "Grace", "Hana", "Hassan",
        "Henry", "Ines", "Isaac", "Isabella", "Ivan", "Jamal", "Jasmine", "Javier",
        "Jonas", "Julia", "Kaito", "Karim", "Kavya", "Kevin", "Kofi", "Laila",
        "Layla", "Leah", "Liam", "Linnea", "Lucas", "Lucia", "Luis", "Marcus",
        "Maria", "Mateo", "Maya", "Meera", "Miguel", "Mila", "Nadia", "Nathan",
        "Nia", "Nikhil", "Noor", "Oliver", "Omar", "Oscar", "Priya", "Quinn",
        "Rafael", "Rania", "Rosa", "Ruth", "Samir", "Samuel", "Sara", "Sebastian",
        "Serena", "Sofia", "Tamara", "Tenzin", "Thomas", "Umar", "Valeria", "Viktor",
        "Vivek", "Wanjiru", "Wei", "Yara", "Yusuf", "Zainab", "Zoe", "Zoey"
    ];

    private static readonly string[] LastNames =
    [
        "Abiodun", "Adeyemi", "Aguilar", "Ali", "Alvarez", "Anderson", "Andersen", "Arora",
        "Baker", "Barnes", "Bello", "Bennett", "Bianchi", "Blanco", "Boateng", "Brooks",
        "Cabrera", "Castillo", "Chandra", "Chatterjee", "Chen", "Clark", "Cohen", "Costa",
        "Dabrowski", "Dahl", "Dalton", "Devi", "Diaz", "Dubois", "Duarte", "Ebadi",
        "El-Sayed", "Eze", "Farah", "Fernandez", "Fischer", "Fitzgerald", "Fleming", "Gallo",
        "Garcia", "Ghosh", "Gomez", "Gonzalez", "Grant", "Gullit", "Gupta", "Haddad",
        "Hansen", "Hassan", "Hernandez", "Hoffman", "Ibrahim", "Iyer", "Janssen", "Jimenez",
        "Kabir", "Karlsson", "Keller", "Khan", "Kim", "Kowalski", "Kruger", "Kumar",
        "Larsen", "Laurent", "Lawal", "Lebedev", "Lindqvist", "Lopez", "Lund", "Mahmoud",
        "Malhotra", "Marchetti", "Martins", "Mbeki", "Mendes", "Meyer", "Mikaelsen", "Mitchell",
        "Morales", "Moreau", "Moser", "Mwangi", "Nakamura", "Navarro", "Nguyen", "Nielsen",
        "Novak", "Okafor", "Olawale", "Olsen", "Osei", "Park", "Patel", "Pavlov",
        "Pettersen", "Pham", "Popov", "Prasad", "Quintero", "Rahman", "Ramirez", "Ramos",
        "Reyes", "Rossi", "Rowe", "Ruiz", "Sanchez", "Santos", "Sato", "Schmidt",
        "Sharma", "Silva", "Singh", "Sokolov", "Sorensen", "Suzuki", "Tanaka", "Tavares",
        "Torres", "Tran", "Vargas", "Vasquez", "Vogel", "Wang", "Weber", "Yilmaz",
        "Yoon", "Zamora", "Zhang", "Zhao", "Zheng", "Zhou"
    ];

    private static readonly string[] EmailDomains =
    [
        "example.com",
        "example.net",
        "example.org"
    ];

    private static readonly string[] StreetNames =
    [
        "Maple Street", "Oak Avenue", "Cedar Lane", "Pine Road", "Elm Drive",
        "Birch Court", "Willow Way", "Spruce Boulevard", "Aspen Circle", "Chestnut Place",
        "Magnolia Street", "Dogwood Avenue", "Poplar Lane", "Sycamore Road", "Walnut Drive",
        "Harborview Drive", "Kingsley Road", "Bridgewater Lane", "Meadowlark Way", "Stonebridge Avenue"
    ];

    private static readonly string[] Cities =
    [
        "Springfield, IL", "Riverton, TX", "Lakeview, OH", "Fairview, OR", "Milton, GA",
        "Arlington, VA", "Greenville, SC", "Burlington, VT", "Clinton, IA", "Dover, DE",
        "Franklin, TN", "Georgetown, KY", "Madison, WI", "Oxford, MS", "Salem, MA"
    ];
}
