# Phonebook .NET 10 — Knowledge Bytes

> Notes explaining the Phonebook .NET 10 project step by step

---

### Byte 1: Overall Project Architecture

**In plain terms:**

This Phonebook project is a full-stack web application. The user manages contacts through the frontend, the frontend sends requests to the ASP.NET Core backend, the backend communicates with the PostgreSQL database, and the result is returned to the frontend. A login system protects the contact data: the frontend signs in once, receives a JWT, and sends it with every request.

**The architecture:**

```text
User
  ↓
Vue.js Frontend (JWT stored in localStorage)
  ↓
ASP.NET Core Backend (JWT verified on protected routes)
  ↓
PostgreSQL Database
  ↓
ASP.NET Core Response
  ↓
Vue.js Frontend
```

**The stack:**

- **Backend:** C# + .NET 10 + ASP.NET Core Minimal API + EF Core + PostgreSQL
- **Frontend:** Vue 3 + Vite + Vue Router + Axios
- **Database:** PostgreSQL 16 (managed by EF Core migrations)
- **Deployment:** Docker Compose (backend, database, frontend services)

---

### Byte 2: Project Structure

**In plain terms:**

The project is divided into separate frontend, backend, database, testing, and Docker-related files. Each folder and file has a specific responsibility.

**The project structure:**

```text
PhonebookDotNet10/
│
├── backend/
│   ├── Program.cs                  # Entry point: DI, middleware, endpoints
│   ├── Phonebook.Api.csproj        # .NET 10 project file
│   ├── appsettings.json            # Configuration
│   ├── Data/
│   │   └── PhonebookDbContext.cs   # EF Core DbContext
│   ├── Endpoints/
│   │   ├── AuthEndpoints.cs        # /auth/login, /auth/me, /auth/logout
│   │   ├── ContactEndpoints.cs     # /contacts CRUD
│   │   ├── CsvEndpoints.cs         # /contacts/import, /contacts/export
│   │   ├── StatsEndpoints.cs       # /stats
│   │   └── HealthEndpoints.cs      # /health
│   ├── Middleware/
│   │   └── ErrorHandlingMiddleware.cs  # Global error handler
│   ├── Models/
│   │   ├── Entities/
│   │   │   ├── Contact.cs          # Contact entity
│   │   │   └── User.cs             # User entity
│   │   └── DTOs/                   # Request/response DTOs
│   ├── Security/
│   │   ├── JwtTokenService.cs      # JWT creation/validation
│   │   └── PasswordHasher.cs       # BCrypt hashing
│   └── Services/
│       ├── AuthService.cs          # Login, user lookup
│       ├── ContactService.cs       # CRUD operations
│       ├── StatsService.cs         # Dashboard statistics
│       ├── CsvImportService.cs     # CSV import logic
│       ├── CsvExportService.cs     # CSV export logic
│       └── SeedService.cs          # Demo data generation
│
├── tests/
│   └── Phonebook.Api.Tests/        # xUnit integration tests
│
├── frontend/                       # Vue 3 app (unchanged)
│   ├── src/
│   │   ├── api.js                  # axios wrapper + interceptors
│   │   ├── auth.js                 # authentication state
│   │   ├── router.js               # routes + auth guard
│   │   └── views/                  # Dashboard, Contacts, etc.
│   ├── Dockerfile
│   └── nginx.conf
│
├── docker-compose.yml
├── .env.example
└── README.md
```

---

### Byte 3: Application Entry Point — Program.cs

**In plain terms:**

`Program.cs` is the main entry point. It configures services (dependency injection), sets up the middleware pipeline, maps all endpoints, runs database migrations, and starts the seed utility.

**The flow:**

```text
1. Create builder (WebApplication.CreateBuilder)
2. Configure JSON (snake_case naming)
3. Register DbContext (EF Core + PostgreSQL)
4. Register JWT authentication
5. Register application services
6. Build the app
7. Add error handling middleware
8. Add authentication/authorization middleware
9. Map all endpoints
10. Run database migrations
11. Run seed utility (if enabled)
12. Start the server
```

---

### Byte 4: Entities — Contact.cs and User.cs

**In plain terms:**

Entities are C# classes that map to database tables. EF Core translates between C# objects and SQL rows.

**The Contact entity:**

```csharp
public class Contact
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

Key points:
- `Id` — auto-increment primary key
- `Name` — required, max 255 chars
- `PhoneNumber` — required, max 20 chars, unique
- `Email` — optional, unique when provided
- `Address` — optional, free text
- `CreatedAt` — set automatically on insert

The `User` entity stores `username` (unique), an optional unique `email`, the BCrypt `HashedPassword`, and `CreatedAt`.

**Important:** Contacts are global — there is no `user_id` column. All authenticated users see the same contacts. This matches the Java reference behavior.

---

### Byte 5: DTOs — Data Transfer Objects

**In plain terms:**

DTOs define the structure of data entering and leaving the API. They are separate from entities so the database schema and the API contract can evolve independently. All DTOs use snake_case JSON naming (via `JsonNamingPolicy.SnakeCaseLower`).

**Key DTOs:**

- `ContactCreateRequest` — payload for creating a contact. Uses Data Annotations: `[Required]` on `Name`, `[RegularExpression]` on `PhoneNumber`, `[EmailAddress]` on `Email`.
- `ContactUpdateRequest` — payload for updating a contact. All fields are optional; only fields explicitly provided are changed (partial update). Each field has a `*Set` flag.
- `ContactResponse` — contact as returned by the API.
- `LoginRequest` — `username` (accepts username or email) + `password`.
- `LoginResponse` — `accessToken`, `tokenType` ("bearer"), and `user`.
- `UserResponse` — user info (never includes the password hash).
- `StatsResponse` — `totalContacts`, `recentContacts`, `withEmail`, `withPhone`, `recentList`.
- `CsvImportResult` — `total`, `imported`, `failed`, `duplicates`, `errors`.
- `CsvRowError` — `row` (file row number) + `errors` (list of messages).
- `ErrorResponse` — `{"detail": "message"}` — the standard error body.

---

### Byte 6: Database Access — PhonebookDbContext

**In plain terms:**

`PhonebookDbContext` is the EF Core DbContext. It defines the database schema and provides `DbSet` properties for querying and saving data.

**The configuration:**

```csharp
public class PhonebookDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();
}
```

The `OnModelCreating` method configures:
- Table names (`users`, `contacts`)
- Column names (snake_case)
- Primary keys (identity columns)
- Unique constraints (username, email, phone_number)
- Indexes
- Default values (`CURRENT_TIMESTAMP` for `created_at`)

---

### Byte 7: Service Layer — Business Logic

**In plain terms:**

Services contain the business logic. Endpoints delegate to services, and services use the DbContext to access the database.

**AuthService** — authentication:
- `LoginAsync` — finds user by username or email, verifies password with BCrypt, creates JWT
- `FindByUsernameAsync` — loads user for `/auth/me`

**ContactService** — CRUD operations:
- `CreateAsync` — saves a new contact; throws `ConflictException` (409) for duplicate phone/email
- `UpdateAsync` — partial update: only fields with their `*Set` flag true are applied
- `DeleteAsync` — finds and deletes a contact; throws `NotFoundException` (404) if not found
- `GetContactAsync` — finds a contact or throws 404
- `SearchAsync` — case-insensitive search by name or phone, paginated, ordered by ID

**StatsService** — dashboard statistics:
- `GetStatsAsync` — total contacts, contacts created in last 7 days, contacts with email, contacts with phone, 5 most recent contacts

**CsvImportService** — CSV import (covered in Byte 10)

**CsvExportService** — CSV export (covered in Byte 11)

**SeedService** — demo data generation (covered in Byte 12)

---

### Byte 8: Endpoints — REST API

**In plain terms:**

Endpoints handle HTTP requests, validate input, call services, and return responses. Each endpoint group maps to a base path.

**HealthEndpoints** — `GET /health` returns `{"status": "ok"}`.

**AuthEndpoints** — authentication:
- `POST /auth/login` — accepts `{"username": "...", "password": "..."}`, returns `{"access_token": "...", "token_type": "bearer", "user": {...}}`
- `GET /auth/me` — returns the currently authenticated user (requires Bearer token)
- `POST /auth/logout` — returns `{"message": "Logged out"}` (client discards the token)

**ContactEndpoints** — contact management:
- `GET /contacts` — list with `page`, `page_size`, `q` query params; sets `X-Total-Count`, `X-Page`, `X-Page-Size` headers
- `POST /contacts` — create (returns 201)
- `GET /contacts/{id}` — retrieve one
- `PUT /contacts/{id}` — partial update
- `DELETE /contacts/{id}` — delete (returns 204)

**StatsEndpoints** — `GET /stats` returns dashboard statistics.

**CsvEndpoints** — CSV import/export:
- `POST /contacts/import` — CSV import (multipart field "file")
- `GET /contacts/export` — CSV export (optional `q` filter)

---

### Byte 9: JWT Authentication

**In plain terms:**

`JwtTokenService` creates and verifies JWT access tokens. Tokens are signed with HS256 and expire after 12 hours (720 minutes). The signing key is derived from the `JWT_SECRET_KEY` environment variable using SHA-256.

**Token creation:**

```csharp
public string CreateToken(string username)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, username),
        new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString())
    };

    var token = new JwtSecurityToken(
        claims: claims,
        expires: now.Add(_expiry).DateTime,
        signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

The token payload uses the standard `sub` (subject) claim for the username and `exp` for the expiry time.

**401 response:** Missing or invalid tokens return `401` with a `WWW-Authenticate: Bearer` header and a `{"detail": "Not authenticated"}` JSON body.

---

### Byte 10: CSV Import

**In plain terms:**

Users can bulk-load contacts by uploading a `.csv` file. The backend parses the file with CsvHelper, validates every row, skips duplicates, and commits all valid rows in one transaction.

**Import rules:**
- File must have a `.csv` extension
- Maximum file size: 5 MB
- Maximum rows: 5,000
- Must be UTF-8 encoded (BOM tolerated)
- Must have `name` and `phone_number` columns (case-insensitive, whitespace-tolerant)
- `email` and `address` columns are optional
- Empty optional cells are stored as `NULL`
- Duplicate phone numbers or emails (existing or within the file) are skipped and counted
- All valid rows are committed in a single transaction

**The response:**

```json
{
  "total": 10,
  "imported": 8,
  "failed": 1,
  "duplicates": 1,
  "errors": [
    {"row": 3, "errors": ["phone_number: must match E.164 format"]}
  ]
}
```

---

### Byte 11: CSV Export

**In plain terms:**

The export downloads every contact — not just the current page — as a CSV file. If a search is active, only matching contacts are exported.

**Formula-injection protection:**

Values that spreadsheet applications could interpret as formulas (starting with `=`, `+`, `-`, or `@`) are prefixed with a single quote. Since every phone number starts with `+`, they are all protected:

```csharp
private static string Sanitize(string? value)
{
    if (string.IsNullOrEmpty(value)) return string.Empty;
    if ("+=−@".IndexOf(value[0]) >= 0)
        return "'" + value;
    return value;
}
```

---

### Byte 12: Seed Data Generation

**In plain terms:**

`SeedService` creates demo data. It runs only when explicitly requested (off by default). It can create the first user and generate dummy contacts.

**Usage:**

```bash
# Create a demo user
dotnet run -- --seed.create-user=true --seed.username=demo --seed.password='your-password'

# Generate dummy contacts
dotnet run -- --seed.count=1000

# Combine both
dotnet run -- --seed.create-user=true --seed.username=demo --seed.count=1000
```

**Safety features:**
- Idempotent: checks if the user already exists before creating
- Unique phone numbers and emails
- Never resets, truncates, or modifies existing data

---

### Byte 13: Error Handling

**In plain terms:**

`ErrorHandlingMiddleware` catches all exceptions and translates them into the standard `{"detail": "message"}` JSON response with the correct HTTP status code.

**Exception mappings:**

| Exception | HTTP Status |
| --- | --- |
| `UnauthorizedException` | 401 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `BadRequestException` | 400 |
| `PayloadTooLargeException` | 413 |
| Other exceptions | 500 |

---

### Byte 14: Pagination and Search

**In plain terms:**

The contact list endpoint supports server-side pagination and case-insensitive search. The frontend sends `page` (1-based), `page_size` (1–100, default 10), and an optional `q` search term.

**The response headers:**

| Header | Value | Meaning |
| --- | --- | --- |
| `X-Total-Count` | e.g. `42` | Total number of matching contacts |
| `X-Page` | e.g. `1` | Current page number |
| `X-Page-Size` | e.g. `10` | Items per page |

The frontend reads `X-Total-Count` to calculate the total number of pages.

---

### Byte 15: Docker Compose

**In plain terms:**

`docker-compose.yml` defines the services required to run the Phonebook application together. The Compose project is named `phonebook-dotnet10`, so its network and volume are completely separate from any other project.

**The services:**

| Service | Image / build | Host port | Internal port |
| --- | --- | --- | --- |
| `db` | `postgres:16-alpine` | (internal) | 5432 |
| `backend` | `./backend` (.NET 10) | 8001 → 8000 | 8000 |
| `frontend` | `./frontend` (Vite + nginx) | 8081 → 80 | 80 |

**Commands:**

```bash
docker compose up -d --build   # Start everything
docker compose ps              # Check status
docker compose logs -f backend # View logs
docker compose down            # Stop (keeps data)
docker compose down -v         # Stop and delete all data
```

---

### Byte 16: The Complete Picture

**In plain terms:**

The Phonebook application is a full-stack system where each layer has a specific responsibility. Vue handles the user interface, ASP.NET Core handles API requests and validation, EF Core handles database interaction, and PostgreSQL stores the data.

**The complete flow:**

```text
User
  ↓
Vue.js Frontend (login page → token in localStorage)
  ↓
api.js (adds Bearer token to every request)
  ↓
ASP.NET Core Endpoint (JWT validated by JwtBearerMiddleware)
  ↓
Service Layer
  ↓
EF Core DbContext
  ↓
PostgreSQL
  ↓
Database Response
  ↓
ASP.NET Core Response ({"detail":"message"} on error)
  ↓
Vue Frontend
  ↓
Updated User Interface
```

**API summary:**

| Endpoint | Purpose | Auth |
| --- | --- | --- |
| `GET /health` | Health check | — |
| `POST /auth/login` | Sign in, returns a JWT | — |
| `GET /auth/me` | Current signed-in user | ✓ |
| `POST /auth/logout` | Sign out (client discards token) | — |
| `GET /stats` | Dashboard statistics + recent contacts | ✓ |
| `GET /contacts` | List contacts (paginated, searchable) | ✓ |
| `POST /contacts` | Add a contact | ✓ |
| `POST /contacts/import` | Import contacts from CSV | ✓ |
| `GET /contacts/export` | Download contacts as CSV | ✓ |
| `GET /contacts/{id}` | Get one contact | ✓ |
| `PUT /contacts/{id}` | Update a contact (partial) | ✓ |
| `DELETE /contacts/{id}` | Delete a contact | ✓ |
