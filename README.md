# Phonebook Application (.NET 10)

A full-stack phonebook app — C# / ASP.NET Core .NET 10 Minimal API backend with the existing Vue 3 frontend.

- **Backend:** C# + .NET 10 + ASP.NET Core Minimal API + EF Core + PostgreSQL
- **Frontend:** Vue 3 + Vite + Vue Router + Axios (unchanged from the Java version)
- **Deployment:** Docker Compose (backend, database, frontend services)

This project is a drop-in replacement for the Java Spring Boot backend. The API contract (routes, status codes, response shapes, pagination headers, error bodies) is identical, so the Vue.js frontend works without any changes.

## Features

- JWT authentication (login with username or email, BCrypt password hashing)
- Add, view, update, and delete contacts (full CRUD via REST API)
- Dashboard with live statistics from the database
- Contact list with search (by name or phone number) and Google-style pagination
- Import contacts from CSV and export contacts to CSV (with per-row validation reports, duplicate handling, and formula-injection protection)
- Input validation on both backend and frontend (E.164 phone format, unique phone/email)
- Seed utility to create the first user and generate dummy contacts
- Docker Compose setup with isolated database volume

## Architecture

```
+----------------+        /api/*         +----------------+        +----------------+
|   Frontend     | <-------------------> |    Backend     | <----> |   PostgreSQL   |
|  Vue 3/nginx   |   nginx proxy_pass    |  ASP.NET Core  |  db:   |   db :5432     |
|  http://:8081  |                       |    :8000       |  5432  |  (pgdata vol.) |
+----------------+                       +----------------+        +----------------+
```

All three services run in Docker Compose with `restart: unless-stopped`.
The Compose project is named `phonebook-dotnet10`, so its network and
volume (`phonebook-dotnet10_pgdata`) are completely separate from the
Java project's resources.

Authentication: the frontend stores the JWT in localStorage and sends it as a
`Authorization: Bearer <token>` header. All contact and stats endpoints require
a valid token; unauthenticated requests get `401` and the app redirects to the
login page. Passwords are stored as BCrypt hashes.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) with the WSL 2 backend (Windows), or Docker Engine + Compose v2 (Linux/macOS)
- Or for local development: .NET 10 SDK, Node.js 20+, PostgreSQL 16

## Quick start (Docker)

1. Create your environment file (database credentials and JWT secret):

   ```bash
   cp .env.example .env
   ```

2. Build and start the application:

   ```bash
   docker compose up --build -d
   ```

3. **Create the first user** (required before you can sign in):

   ```bash
   docker compose run --rm \
     -e Seed__CreateUser=true -e Seed__Username=demo \
     -e Seed__Password='your-password' backend
   ```

   A throw-away container is used on purpose: `docker compose exec backend`
   would start a *second* copy of the app inside the running container, which
   cannot bind to port 8000.

4. Open the application:

   | URL | What |
   | --- | --- |
   | <http://localhost:8081> | Frontend (Vue 3 app) |
   | <http://localhost:8001> | Backend API (ASP.NET Core) |
   | <http://localhost:8001/health> | Health check |

5. Stop the application:

   ```bash
   docker compose down
   ```

## Docker services

| Service   | Image / build                    | Host port | Internal port | Notes                                    |
| --------- | -------------------------------- | --------- | ------------- | ---------------------------------------- |
| `frontend` | `./frontend` (Vite build to nginx) | 8081 → 80 | 80            | nginx proxies `/api/` to the backend     |
| `backend`  | `./backend` (.NET 10)            | 8001 → 8000 | 8000          | ASP.NET Core; connects to PostgreSQL      |
| `db`       | `postgres:16-alpine`              | (internal) | 5432         | Persistent data in the `pgdata` volume   |

### PostgreSQL configuration

Configured through `.env` (see `.env.example`):

| Variable         | Default         | Where used                                          |
| ---------------- | --------------- | --------------------------------------------------- |
| `POSTGRES_USER`  | `phonebook`     | superuser created by the postgres image             |
| `POSTGRES_PASSWORD` | `change-me`  | password for that user                              |
| `POSTGRES_DB`    | `phonebook_dotnet10` | database created on first startup               |
| `JWT_SECRET_KEY` | (empty)         | JWT signing key; set a long random string in `.env` |
| `BACKEND_PORT`   | `8001`          | host port the backend publishes                     |
| `FRONTEND_PORT`  | `8081`          | host port nginx publishes                           |
| `SEED_*`         | disabled        | optional first user / dummy contacts                |

- The backend connects to `Host=db;Port=5432;Database=phonebook_dotnet10` — the Compose **service name** `db`, never `localhost`.
- PostgreSQL data persists in the `phonebook-dotnet10_pgdata` Docker volume.
- The backend waits for PostgreSQL via `depends_on: condition: service_healthy`.
- EF Core migrations run automatically on startup (safe, non-destructive) and only apply pending migrations.
- If `JWT_SECRET_KEY` is empty, the backend generates an ephemeral random key on each start (fine for demos, but users must log in again after a restart).
- Host ports are `8001` (API) and `8081` (frontend) so they never collide with the Java Phonebook app on `8000`/`8080`.

## API reference

| Method | Endpoint            | Description                                            | Auth |
| ------ | ------------------- | ------------------------------------------------------ | ---- |
| GET    | `/health`           | Health check                                           | —    |
| POST   | `/auth/login`       | Login with username or email, returns a JWT            | —    |
| GET    | `/auth/me`          | Currently logged-in user                               | ✓    |
| POST   | `/auth/logout`      | Logout (client discards the token)                     | —    |
| GET    | `/stats`            | Dashboard statistics + 5 most recent contacts          | ✓    |
| GET    | `/contacts`         | List contacts (`page`, `page_size`, `q` params; total in `X-Total-Count` header) | ✓    |
| POST   | `/contacts`         | Add a new contact                                      | ✓    |
| POST   | `/contacts/import`  | Import contacts from an uploaded CSV file              | ✓    |
| GET    | `/contacts/export`  | Download contacts as CSV (`q` param filters the export) | ✓    |
| GET    | `/contacts/{id}`    | Retrieve a specific contact                            | ✓    |
| PUT    | `/contacts/{id}`    | Update a specific contact (only provided fields change) | ✓    |
| DELETE | `/contacts/{id}`    | Delete a specific contact                              | ✓    |

Call an authenticated endpoint with the header `Authorization: Bearer <access_token>`.

Request/response example (`POST /contacts`):

```json
{
  "name": "John Doe",
  "phone_number": "+1234567890",
  "email": "john@example.com",
  "address": "123 Main St"
}
```

Validation rules:
- `name` is required (max 255 chars)
- `phone_number` is required, unique, and must match E.164 format, e.g. `+1234567890`
- `email` is optional but must be a valid email and unique when provided
- `address` is optional

Error responses (always a JSON `{"detail": "..."}` body):

| Status | When |
| ------ | ---- |
| `400` | Validation error on a request body, malformed JSON, or a CSV format problem |
| `401` | Missing/invalid token, or wrong login credentials (`WWW-Authenticate: Bearer`) |
| `404` | Contact not found, or an unknown route |
| `405` | Wrong HTTP method for a known route |
| `409` | Duplicate phone number or email |
| `413` | CSV larger than 5 MB or more than 5000 rows |
| `500` | Unexpected server error |

## Authentication

- **Login** — `POST /auth/login` with `{"username": "...", "password": "..."}`
  (the `username` field accepts a username *or* an email). Returns
  `{"access_token", "token_type": "bearer", "user": {...}}`.
- Passwords are hashed with **BCrypt**.
- Tokens are **JWTs** signed with **HS256** using `JWT_SECRET_KEY` (from `.env`) and expire
  after 12 hours. The frontend stores the token in localStorage and
  sends it on every request; a `401` response logs the user out.
- Contact and stats endpoints require the header
  `Authorization: Bearer <token>`.
- Missing or invalid tokens return `401` with a `WWW-Authenticate: Bearer`
  header and a `{"detail": "Not authenticated"}` JSON body.

**Create the first/demo user** (there are no users by default):

```bash
docker compose run --rm \
  -e Seed__CreateUser=true -e Seed__Username=demo \
  -e Seed__Password='your-password' backend
```

Or with the .NET CLI locally:

```bash
dotnet run -- --seed.create-user=true --seed.username=demo --seed.password='your-password'
```

## Seeding dummy data

The seed utility generates **realistic dummy contacts** (full names, unique
E.164 phone numbers, unique email addresses on the reserved `example.*`
domains, and varied street/city/state addresses).

Guarantees:
- Only **inserts** contacts — existing contacts and users are never updated,
  deleted, or overwritten.
- Phone numbers and email addresses are unique **against the database and
  within the generated batch**; colliding candidates are discarded and
  replaced until the requested number of contacts is reached.
- Never creates contact-owner accounts; `Seed__CreateUser` is the only way to
  add a user and defaults to off.

### Recommended: one command

```bash
# Windows (PowerShell)
.\scripts\seed-contacts.ps1 -Count 1000

# Linux / macOS
./scripts/seed-contacts.sh 1000
```

The script starts a throw-away container (`Seed__Exit=true`), seeds, shuts that
container down, and prints the counts before and after. The long-running
backend and frontend are not restarted.

### Equivalent raw command

```bash
docker compose run --rm \
  -e Seed__Count=1000 \
  -e Seed__Exit=true \
  backend
```

`Seed__Exit=true` makes the container run migrations + the seed and then exit
instead of starting Kestrel.

### Creating a user

```bash
docker compose run --rm \
  -e Seed__CreateUser=true -e Seed__Username=demo \
  -e Seed__Password='your-password' -e Seed__CreateUser=true backend
```

Both actions can be combined in a single one-off container as well.

The seed operation is manual — it never runs automatically on startup.

## CSV import & export

### Importing contacts

The CSV must have a header row with these columns:

```csv
name,phone_number,email,address
Rahul Sharma,+919876543210,rahul@example.com,Pune
Amit Patil,+919876543211,amit@example.com,Mumbai
```

| Column        | Required | Rules (same as the web form)                              |
| ------------- | -------- | --------------------------------------------------------- |
| `name`        | Yes      | 1-255 characters                                          |
| `phone_number`| Yes      | Unique, E.164 format (`+` followed by 1-15 digits)        |
| `email`       | No       | Valid email, unique when provided; empty cell = no email  |
| `address`     | No       | Free text; empty cell = no address                        |

Column names are matched case-insensitively and tolerate surrounding
whitespace; extra columns are ignored, and `email`/`address` columns may
be omitted entirely.

**Limits.** Uploads are capped at **5 MB** and **5,000 rows** per file
(larger files are rejected with `413`). All valid, non-duplicate rows are
inserted in a **single transaction** — a database error rolls back the
entire import.

### Exporting contacts

The export contains **every contact** — not just the current page — and
respects an active search. Empty result sets download a header-only CSV.

**CSV formula-injection protection:** values beginning with `=`, `+`, `-`,
or `@` (which includes every phone number, since they start with `+`) are
prefixed with a single quote (`'`) so spreadsheet applications treat them
as text instead of formulas.

## Local development (without Docker)

### Backend

```bash
cd backend
dotnet run
```

`dotnet run` uses `Properties/launchSettings.json`, so the API listens on
`http://localhost:8000` and runs in the Development environment (OpenAPI at
`/openapi/v1.json`).

The database defaults to
`Host=localhost;Database=phonebook_dotnet10;Username=phonebook;Password=phonebook`
(a database that is completely separate from the Java project). Override it
with environment variables:

```bash
# Windows (PowerShell)
$env:ConnectionStrings__DefaultConnection="Host=localhost;Database=phonebook_dotnet10;Username=phonebook;Password=phonebook"
$env:JWT_SECRET_KEY="your-long-random-secret"

# Windows (CMD)
set ConnectionStrings__DefaultConnection=Host=localhost;Database=phonebook_dotnet10;Username=phonebook;Password=phonebook
set JWT_SECRET_KEY=your-long-random-secret

# Linux / macOS
export ConnectionStrings__DefaultConnection='Host=localhost;Database=phonebook_dotnet10;Username=phonebook;Password=phonebook'
export JWT_SECRET_KEY='your-long-random-secret'

dotnet run
```

A local PostgreSQL you have not started yet can be launched with Docker
without touching any other project's database:

```bash
docker run -d --name phonebook-dotnet10-local-db \
  -e POSTGRES_USER=phonebook -e POSTGRES_PASSWORD=phonebook \
  -e POSTGRES_DB=phonebook_dotnet10 -p 5432:5432 postgres:16-alpine
```

Create the first user and seed data:

```bash
dotnet run -- --seed.create-user=true --seed.username=demo --seed.password='your-password'
dotnet run -- --seed.count=1000
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server proxies `/api` requests to `http://localhost:8000`
(the local `dotnet run` backend). Point it somewhere else with
`VITE_API_PROXY_TARGET`:

```bash
VITE_API_PROXY_TARGET=http://localhost:8001 npm run dev
```

## Tests

```bash
cd tests/Phonebook.Api.Tests
dotnet test
```

The test suite covers:
- Login with username and email, wrong credentials, invalid/missing JWTs
- Current-user endpoint and authorization (401s with `WWW-Authenticate: Bearer`)
- Dashboard statistics
- Contact CRUD, validation, duplicate handling (409)
- Search and pagination, including `X-Total-Count` / `X-Page` / `X-Page-Size` headers
- CSV import: valid data, invalid rows, duplicates, malformed files, size/row limits
- CSV export: all pages, search filtering, empty results, escaping, formula-injection protection

Tests run against an **isolated in-memory database** — no PostgreSQL required.
Each test gets its own store, so the suite does not depend on test ordering.

To run the whole solution at once:

```bash
dotnet test tests/Phonebook.Api.Tests/Phonebook.Api.Tests.csproj
```

To build everything (backend + tests):

```bash
dotnet build tests/Phonebook.Api.Tests/Phonebook.Api.Tests.csproj
```

## Project structure

```
PhonebookDotNet10/
├── docker-compose.yml        # backend + db + frontend services (isolated project)
├── .env.example              # environment variables (database credentials, JWT secret, ports)
├── .github/workflows/ci.yml # GitHub Actions: backend tests + frontend build
├── backend/
│   ├── Dockerfile            # multi-stage .NET 10 build
│   ├── .dockerignore
│   ├── Phonebook.Api.csproj
│   ├── Program.cs            # entry point, DI, middleware, endpoints
│   ├── appsettings.json
│   ├── Data/
│   │   ├── PhonebookDbContext.cs  # EF Core DbContext
│   │   └── DesignTimeDbContextFactory.cs  # used by `dotnet ef`
│   ├── Migrations/           # EF Core migrations (applied on startup)
│   ├── Endpoints/            # Auth, Contact, Stats, CSV, Health endpoints
│   ├── Middleware/           # Error handling middleware
│   ├── Models/
│   │   ├── Entities/         # Contact, User
│   │   └── DTOs/             # request/response DTOs (snake_case JSON)
│   ├── Security/             # JWT token service, BCrypt password hasher
│   ├── Validation/           # request DTO validation filter
│   └── Services/             # Auth, Contact, Stats, CSV import/export, Seed
├── tests/
│   └── Phonebook.Api.Tests/  # xUnit integration tests (WebApplicationFactory)
├── scripts/
│   └── seed-contacts.ps1|sh # reusable dummy-contact seeder
└── frontend/                 # Vue 3 app
    ├── Dockerfile
    ├── nginx.conf
    └── src/
```

## Database schema

Managed by EF Core migrations (auto-applied on startup):

**users**: `id` (identity PK), `username` (varchar 100, unique, indexed),
`email` (varchar 255, unique, nullable), `hashed_password` (varchar 255),
`created_at` (timestamptz, default now())

**contacts**: `id` (identity PK), `name` (varchar 255), `phone_number`
(varchar 20, unique, indexed), `email` (varchar 255, unique, nullable),
`address` (text, nullable), `created_at` (timestamptz, default now())

## Troubleshooting

- **"Port already in use"**: change `BACKEND_PORT` / `FRONTEND_PORT` in `.env`
  (defaults are `8001` and `8081`, chosen to avoid the Java project's
  `8000`/`8080`).
- **Tokens invalid after restart**: set `JWT_SECRET_KEY` in `.env`.
- **Reset the database**: `docker compose down -v` (deletes the `pgdata`
  volume), then `docker compose up --build`. *Never* run this against another
  project's volume — the name `phonebook-dotnet10_pgdata` is already
  project-specific.
- **Check logs**: `docker compose logs backend`, `docker compose logs db`,
  `docker compose logs frontend`.
- **"Invalid request body"** on login: the client sent malformed JSON or
  omitted `username`/`password`.
- **CORS errors**: the API accepts any origin. When running the frontend
  against a locally started backend, set `VITE_API_PROXY_TARGET` so the Vite
  proxy targets the right port.
