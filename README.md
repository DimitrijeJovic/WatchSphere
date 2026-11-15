# WatchSphere

A full-stack media catalogue and personal watch tracker for anime, movies, and television series built with ASP.NET Core (.NET 10) and Vue 3.

WatchSphere consolidates title discovery, personal progress tracking, scoring, reviews, and cross-catalogue metadata imports into a single unified application.

<!-- Screenshot placeholder: Catalogue and Library Views (TODO) -->

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

---

## 5. Prerequisites

Before running the application locally, ensure you have the following installed:
- **.NET SDK**: .NET 10.0 SDK (version `10.0.200` or higher; configured via `global.json` with `latestFeature` roll-forward)
- **Node.js**: Version 18.x or later with `npm`
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
The application automatically checks and executes pending migrations on startup via `DatabaseSeeder.SeedAsync()` if the configured database user holds DDL permissions. 

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

The repository has undergone structured pre-publication audit and runtime verification. Tested functionality is strictly delineated from unverified areas:

### Verified Build & Security Checks
- **Backend Build**: Compiled with .NET 10 SDK (`0 Warning(s)`, `0 Error(s)`).
- **Frontend Build**: Production asset build passed via `npm run build` with chunked output in ~460ms.
- **Dependency Vulnerabilities**: Upgraded transitive `MySqlConnector` dependency to `2.6.1`, resolving moderate severity advisory [GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8). Confirmed `0 vulnerable packages` via `dotnet list package --vulnerable`.
- **Repository Hygiene**: Confirmed zero tracked credentials or secret leaks; development backups, `.env`, and local settings files are verified excluded by `.gitignore`.

### Verified Runtime Functionality
- **API Health**: `GET /health` verified returning HTTP 200.
- **Database Migrations**: Verified EF Core migration execution against local MySQL 8.
- **Authentication**: `POST /api/Auth/register` and `POST /api/Auth/login` verified returning HTTP 200 with JWT tokens carrying role claims.
- **TMDB Integration**: Verified `GET /api/Import/search` returning HTTP 200 with active search results across movies (10 results), TV series (10 results), and combined queries (33 results).

### Testing Boundaries & Unverified Items
- **Automated Tests**: There is currently no dedicated automated test project (`xUnit`/`NUnit`). Integration tests were executed via direct API endpoint verification.
- **Frontend E2E**: While components build without errors and local development flows function, end-to-end automated headless browser tests (Playwright/Cypress) have not been run.

Detailed logs and historical findings are documented in [`docs/AUDIT_REPORT.md`](docs/AUDIT_REPORT.md) and [`docs/TEST_REPORT.md`](docs/TEST_REPORT.md).

---

## 10. Current Limitations & Roadmap

The following design decisions and limitations exist in the current development preview:
1. **Self-Service Password Reset**: Users cannot reset or change passwords through self-service endpoints; password resets currently require administrative or direct database intervention.
2. **Refresh Token Flow**: Authentication relies on 24-hour stateless JWT access tokens without refresh token rotation.
3. **Automated Test Suite**: Formal unit and integration test coverage across services and controllers is planned for an upcoming release.
4. **Third-Party Rate Limits**: Ingestion and synchronization are bounded by TMDB and TVmaze public API rate limits.
