# WatchSphere

A full-stack media catalogue and personal watch tracker for anime, movies, and television series built with ASP.NET Core (.NET 10) and Vue 3.

WatchSphere consolidates title discovery, personal progress tracking, scoring, reviews, and cross-catalogue metadata imports into a single unified application.

The concept and feature design are my own; the implementation was written with AI assistance. Development ran from June to September 2026 (see the dates of the EF Core migrations); the public repository was published as a single commit.

---

## 1. Project Overview & Purpose

Media tracking is often fragmented across specialized services: anime is typically catalogued on MyAnimeList or AniList, while films and western television series are tracked on platforms like Letterboxd or Trakt. 

WatchSphere was created as an integrated, self-hosted tracking platform with three core objectives:
1. **Unified Tracking**: Track anime, movies, and television shows in one personal library with a consistent data model.
2. **Multi-Source Ingestion & Deduplication**: Aggregate metadata across multiple independent providers (The Movie Database, TVmaze, Kitsu, and IMDb) while avoiding duplicated entries through normalized cross-catalogue matching.
3. **Flexible Progress Tracking**: Support both simple increment tracking (MyAnimeList-style `+1` counters) and granular episode ticking with season and air-date awareness.

---

## 2. Implemented Features

### Unified Catalogue & Multi-Provider Search
- **Federated Search**: Search across TMDB (movies and television), TVmaze (western TV), and Kitsu (anime) through a unified provider interface (`ICatalogProvider`).
- **Normalized Deduplication**: Resolves incoming records against existing titles by matching IMDb IDs first, followed by provider-specific external IDs, and finally normalized title and release year.
- **Batched Catalogue Imports**: Administrative tools to import trending or top-rated batches directly from active providers without manually keying data.

### Personal Library & Episode Tracking
- **Five Tracking States**: Manage personal lists categorized as `Watching`, `Completed`, `Planned`, `OnHold`, and `Dropped`.
- **Dual-Mode Progress**:
  - *Counter Mode*: Fast `+1` increment directly from title cards and posters.
  - *Episode Checklist*: Detailed per-season breakdown tracking individual episodes with titles, air dates, stills, and watched status, including an *Up to here* bulk-tick action.
- **Automated State Progression**: Starting an episode automatically transitions a planned title to `Watching`; ticking the final episode transitions it to `Completed` and records the finish timestamp.

### Scores, Reviews & Discussions
- **Side-by-Side Ratings**: Title pages compare three separate evaluation metrics: community member scores, provider scores, and IMDb ratings.
- **IMDb Rating Ingestion**: Streams daily official IMDb TSV rating dumps (`datasets.imdbws.com`) and updates database records without loading large datasets into memory.
- **Public Reviews & Private Scores**: Separates a user's private library score from public text reviews. Reviews support community voting (upvote/downvote) and threaded comments.

### Franchises & Filmographies
- **Bidirectional Relation Graph**: Visualizes relations such as sequels, prequels, spin-offs, side stories, and parent film collections.
- **Cast & Crew Directory**: Normalizes billing order, character assignments (prioritizing Japanese voice actors for anime and core creative crew for films), with dedicated filmography pages per person.

### Background Synchronization
- **Automated Refresh (`CatalogSyncService`)**: A daily background service checks airing shows and older titles for metadata or episode updates, paced to respect external provider rate limits.

---

## 3. Technology Stack

### Backend
- **Framework**: ASP.NET Core 10 (Web API) on .NET 10 (`net10.0`)
- **ORM & Database Provider**: Entity Framework Core 9 (`9.0.0`) with Pomelo MySQL Provider (`9.0.0`)
- **Database Driver**: `MySqlConnector` (`2.6.1`) explicitly pinned to address security advisory GHSA-473q-m89c-ghf8
- **Authentication**: JWT Bearer Authentication (`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9) with HMAC-SHA256 signatures
- **Password Hashing**: `BCrypt.Net-Next` (`4.2.0`)
- **API Documentation**: OpenAPI / Swagger (`Swashbuckle.AspNetCore` 10.2.1)
- **Rate Limiting**: Built-in ASP.NET Core IP-based fixed-window partition limiting on authentication routes

### Frontend
- **Framework**: Vue 3 (`3.5.34`) utilizing the Composition API (`<script setup>`)
- **Routing**: Vue Router (`5.1.0`) with chunked route-level lazy loading
- **HTTP Client**: Axios (`1.17.0`) configured with request/response interceptors for centralized JWT attachment and 401 session clearance
- **Build Tooling**: Vite (`8.0.12`) with `@vitejs/plugin-vue` (`6.0.6`)
- **Styling**: Vanilla CSS using custom design tokens and tabular monospace metrics for count alignment

### External Services
- **The Movie Database (TMDB)**: REST API (v3) for films, series, and collection data
- **TVmaze**: REST API for television broadcast schedules and cast data
- **Kitsu**: JSON:API for anime metadata and relationship graphs
- **IMDb**: Daily TSV dumps for public rating aggregates

---

## 4. Architecture & Project Structure

The project follows a decoupled client-server architecture with separation of concerns:

```
WatchSphere/
├── WatchSphere.API/                  # ASP.NET Core Web API
│   ├── Controllers/                  # HTTP route endpoints (Auth, Media, Library, Import, etc.)
│   ├── Data/                         # EF Core DbContext and DatabaseSeeder
│   ├── DTOs/                         # Request and response data transfer contracts
│   ├── Extensions/                   # ClaimsPrincipal and helper extensions
│   ├── Interfaces/                   # Service and provider abstractions
│   ├── Middleware/                   # Global exception handling and problem details
│   ├── Migrations/                   # EF Core Code-First database migrations
│   ├── Models/                       # Domain database entities
│   ├── Properties/                   # launchSettings.json profile configs
│   ├── Services/                     # Business logic (Library, Catalog, Sync, JWT, IMDb)
│   │   └── Providers/                # External catalog adapters (TMDB, Kitsu, TVmaze)
│   ├── appsettings.json              # Public base configuration (placeholders only)
│   └── Program.cs                    # Application bootstrapping and DI composition
│
├── WatchSphere.Tests/                # xUnit tests (API over HTTP and service logic, SQLite in memory)
│   ├── Api/                          # Authentication, authorization, validation, rate limiting
│   ├── Services/                     # Library progress and status rules
│   └── Infrastructure/               # Test host (WebApplicationFactory) and test database
│
├── WatchSphere.Frontend/             # Vue 3 Single Page Application
│   ├── public/                       # Static public assets (favicon.svg)
│   ├── src/
│   │   ├── api/                      # Axios HTTP client configuration and error parsing
│   │   ├── components/               # Reusable UI components (MediaCard, Tracker, Guide, etc.)
│   │   ├── router/                   # Client-side route definitions
│   │   ├── stores/                   # Shared reactive state (auth, library)
│   │   ├── views/                    # Primary application views (Home, Browse, Admin, etc.)
│   │   ├── App.vue                   # Root component shell
│   │   ├── main.js                   # Client entrypoint
│   │   └── style.css                 # Global theme variables and base typography
│   ├── .env.example                  # Environment template for API base URL
│   └── vite.config.js                # Vite build configuration
│
└── docs/                             # Engineering and audit reports
    ├── ARCHITECTURE.md               # In-depth architectural documentation
    ├── AUDIT_REPORT.md               # Full codebase security and quality audit log
    ├── FINAL_RELEASE_REPORT.md       # Pre-publication checklist and verification log
    └── TEST_REPORT.md                # Build and runtime test verification matrix
```

### Architectural Highlights
- **Database-Enforced Integrity**: Unique constraints protect data integrity directly in MySQL (`Users.Email`, `Users.Username`, `Genres.Name`, and composite keys on `UserLibraries(UserId, MediaId)` and `Reviews(UserId, MediaId)`).
- **Stateless Authentication**: JWT tokens carry validated user identity and role claims (`User`, `Admin`). Library endpoints derive the user ID exclusively from claims rather than untrusted client URL parameters.
- **Resilient Ingestion**: String values from third-party APIs are sanitized and truncated at word boundaries before persistence to satisfy database column constraints.

### Authentication Overview
- `POST /api/Auth/register` and `POST /api/Auth/login` return a JWT signed with HMAC-SHA256. Passwords are stored as BCrypt hashes; login returns the same error for an unknown email and a wrong password.
- The signing key comes from `WATCHSPHERE_JWT_KEY` (or `Jwt:Key`); the API refuses to start if it is missing or shorter than 32 bytes. Tokens are checked for issuer, audience, signature, and expiry with no clock skew.
- Catalogue, genre, import, and user-management writes require the `Admin` role; library, episode progress, and reviews require a signed-in user. A review can be edited only by its author and deleted by its author or an administrator.
- Register and login are limited to 10 requests per minute per client address.

---

## 5. Prerequisites

Before running the application locally, ensure you have the following installed:
- **.NET SDK**: .NET 10.0 SDK (version `10.0.200` or higher; configured via `global.json` with `latestFeature` roll-forward)
- **Node.js**: Version 20.19 or later, or 22.12 or later, with `npm` (required by Vite 8)
- **Database Server**: MySQL 8.0 or compatible server running locally on port 3306
- **TMDB API Credentials** *(Optional)*: Free TMDB account and API key / Read Access Token (required only for film imports; anime and TVmaze series work without credentials)

---

## 6. Environment & Configuration

WatchSphere enforces a strict separation between public configuration templates and local secrets. **Never place real credentials into the tracked `appsettings.json` file.**

### Configuration Hierarchy
The application reads configuration from the following sources, in order of precedence:
1. System environment variables
2. `WatchSphere.API/appsettings.Development.json` (gitignored, for local development overrides)
3. `WatchSphere.API/appsettings.json` (tracked public repository baseline)

### Environment Variables

| Variable | Required | Description |
|---|:---:|---|
| `WATCHSPHERE_JWT_KEY` | **Yes** | Cryptographic signing key (minimum 32 characters / bytes) |
| `WATCHSPHERE_ADMIN_PASSWORD` | **First Run** | Initial password assigned to the seeded administrative user |
| `WATCHSPHERE_TMDB_KEY` | Optional | TMDB API Read Access Token (Bearer) or API key for movie imports |
| `ConnectionStrings__DefaultConnection` | Optional | Database connection string override |

### Safe Local Development Setup

To configure local development credentials without risk of committing secrets:

1. In the `WatchSphere.API` directory, create `appsettings.Development.json` (this file is excluded by `.gitignore`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=watchsphere;user=watchsphere_app;password=YOUR_LOCAL_DB_PASSWORD"
  },
  "Jwt": {
    "Key": "REPLACE_WITH_GENERATED_KEY_MINIMUM_32_BYTES"
  },
  "Tmdb": {
    "ApiKey": "YOUR_TMDB_API_READ_ACCESS_TOKEN"
  },
  "Seed": {
    "AdminPassword": "YOUR_INITIAL_ADMIN_SEED_PASSWORD"
  }
}
```

2. Generate a cryptographically secure 64-byte key for `WATCHSPHERE_JWT_KEY` via PowerShell without displaying it:

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
setx WATCHSPHERE_JWT_KEY ([Convert]::ToBase64String($bytes))
```

---

## 7. Database Setup & Migrations

WatchSphere requires a MySQL database named `watchsphere`.

### Database Privileges
For production environments, separate migration privileges from daily application runtime operations:
- **Migration Account** (used when running schema updates):
  ```sql
  GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES ON watchsphere.* TO 'watchsphere_migrator'@'localhost';
  ```
- **Runtime Application Account** (standard least-privilege operation):
  ```sql
  GRANT SELECT, INSERT, UPDATE, DELETE ON watchsphere.* TO 'watchsphere_app'@'localhost';
  ```

### Running Migrations
On MySQL, the application applies pending migrations on startup via `DatabaseSeeder.SeedAsync()` if the configured database user holds DDL permissions. (The migrations contain MySQL-specific SQL; with any other provider, such as the SQLite database used by the tests, the schema is created directly from the model instead.)

Alternatively, apply migrations manually using the .NET EF CLI from the `WatchSphere.API` directory:
```bash
dotnet ef database update
```

On initial startup against an empty database, default genres are automatically populated, and an administrator account is seeded using `Seed:AdminUsername` and `WATCHSPHERE_ADMIN_PASSWORD`.

---

## 8. How to Run

### 1. Run the Backend API

```bash
cd WatchSphere.API
dotnet run
```
- **Base URL**: `http://localhost:5214`
- **Swagger Documentation**: `http://localhost:5214/swagger`
- **Health Check Endpoint**: `http://localhost:5214/health`

### 2. Run the Frontend Client

In a separate terminal:
```bash
cd WatchSphere.Frontend
npm install
npm run dev
```
- **Client URL**: `http://localhost:5173`
- If the backend is running on a custom port or remote host, copy `.env.example` to `.env` and set `VITE_API_URL` accordingly:
  ```bash
  cp .env.example .env
  ```

---

## 9. Testing & Verified Results

### Automated Tests

```bash
dotnet test WatchSphere.Tests
```

21 xUnit tests. The API tests host the real application in memory with `WebApplicationFactory`, in a `Testing` environment (local development settings are not loaded, the background sync service is removed, and a throwaway JWT key is used). Data is stored in an in-memory SQLite database, so unique indexes and foreign keys are enforced. No MySQL server or external API is needed.

| Area | What is checked |
|---|---|
| Authentication | Register returns a working token; passwords stored as BCrypt hashes; emails normalised; duplicate email returns 409; short password returns 400; identical 401 for unknown email and wrong password |
| Authorization | 401 without a token, with a token signed by another key, and with an expired token; 403 for a regular user on an admin endpoint; review editing limited to the author, deletion to the author or an administrator |
| Validation | An unknown library status is rejected with 400 |
| Rate limiting | The eleventh login attempt within a minute returns 429 |
| Library rules | Duplicate and unknown titles refused; first episode starts a planned title; last episode completes it; progress clamped to the episode count; "Planned" clears progress; a movie counts as one episode; another user's entry cannot be changed or removed |

Not covered: the MySQL migrations themselves, the external catalogue providers, the background sync service, and the Vue frontend.

### Earlier Manual Checks (Historical)

The following were recorded during the pre-publication review in September to November 2026 and have not been repeated since; see `docs/TEST_REPORT.md`.

### Verified Build & Security Checks
- **Backend Build**: Compiled with .NET 10 SDK (`0 Warning(s)`, `0 Error(s)`).
- **Frontend Build**: Production asset build passes via `npm run build` (re-checked on 17 November 2026).
- **Dependency Vulnerabilities**: Upgraded transitive `MySqlConnector` dependency to `2.6.1`, resolving moderate severity advisory [GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8). Confirmed `0 vulnerable packages` via `dotnet list package --vulnerable`.
- **Repository Hygiene**: Confirmed zero tracked credentials or secret leaks; development backups, `.env`, and local settings files are verified excluded by `.gitignore`.

### Verified Runtime Functionality
- **API Health**: `GET /health` verified returning HTTP 200.
- **Database Migrations**: Verified EF Core migration execution against local MySQL 8.
- **Authentication**: `POST /api/Auth/register` and `POST /api/Auth/login` verified returning HTTP 200 with JWT tokens carrying role claims.
- **TMDB Integration**: Verified `GET /api/Import/search` returning HTTP 200 with active search results across movies (10 results), TV series (10 results), and combined queries (33 results).

### Testing Boundaries & Unverified Items
- **Frontend**: The frontend builds without errors, but it has no automated tests, and no end-to-end browser tests (Playwright/Cypress) exist.
- **Frontend dependencies**: `npm audit` is not clean (checked on 17 November 2026). The lockfile published before the dependency update resolves older versions of Axios (1.17.0) and Vite (8.0.16), and of the transitive packages follow-redirects, nanoid, PostCSS, source-map-js and `@vue/server-renderer`, all with known advisories. The updated lockfile (Axios 1.21.1, Vite 8.3.8, Vue 3.5.45) resolves those, but one high-severity advisory in `picomatch` ([GHSA-fc5q-39xc-vjpf](https://github.com/advisories/GHSA-fc5q-39xc-vjpf)) remains because no patched release exists yet. picomatch is used only by the build tooling and is not part of the production bundle.

Detailed logs and historical findings are documented in [`docs/AUDIT_REPORT.md`](docs/AUDIT_REPORT.md) and [`docs/TEST_REPORT.md`](docs/TEST_REPORT.md).

---

## 10. Current Limitations & Roadmap

The following design decisions and limitations exist in the current development preview:
1. **Self-Service Password Reset**: Users cannot reset or change passwords through self-service endpoints; password resets currently require administrative or direct database intervention.
2. **Refresh Token Flow**: Authentication relies on 24-hour stateless JWT access tokens without refresh token rotation.
3. **Test Coverage**: Automated tests cover authentication, authorization, validation, rate limiting, and library rules (section 9). Providers, synchronization, MySQL-specific behaviour, and the frontend are not covered.
4. **Third-Party Rate Limits**: Ingestion and synchronization are bounded by TMDB and TVmaze public API rate limits.
