# WatchSphere — Architecture

## System Overview

```
┌─────────────────┐     HTTP/JSON     ┌─────────────────┐
│  Vue 3 SPA      │ ◄──────────────► │  ASP.NET Core   │
│  (Vite 8)       │                   │  REST API       │
│  port 5173      │                   │  port 5214      │
└─────────────────┘                   └────────┬────────┘
                                               │
                                    ┌──────────┼──────────┐
                                    │          │          │
                                    ▼          ▼          ▼
                              ┌──────────┐ ┌──────┐ ┌──────────┐
                              │ MySQL 8  │ │ TMDB │ │ Kitsu /  │
                              │ EF Core 9│ │ API  │ │ TVmaze   │
                              └──────────┘ └──────┘ └──────────┘
```

## Backend Structure

### Layer Responsibilities

```
Controllers/         ← HTTP routing, input validation, response shaping
  AuthController       Authentication (register, login, profile)
  MediaController      Catalogue browse, search, detail
  UserLibraryController  Personal library CRUD
  ReviewController     Reviews, votes, comments
  EpisodeController    Episode-level progress tracking
  ImportController     External catalogue import (admin)
  UserController       User management (admin)
  GenreController      Genre CRUD (admin)
  PersonController     Cast/crew lookup

Services/            ← Business logic
  JwtService           Token generation, signing key management
  LibraryService       Progress tracking, status transitions
  MediaService         Media CRUD with genre sync
  EpisodeProgressService  Episode watched state, batch operations
  CatalogImportService   Multi-provider import, deduplication
  CatalogSyncService     Background nightly refresh
  ImdbRatingService      IMDb dataset streaming and matching

Interfaces/          ← Service contracts
  ICatalogProvider       Unified provider interface (TMDB, Kitsu, TVmaze)
  IMediaService, ILibraryService, etc.

Providers/           ← External catalogue adapters
  TmdbProvider           TMDB v3/v4 integration
  KitsuProvider          Kitsu JSON:API integration
  TvMazeProvider         TVmaze integration

Data/                ← Persistence
  AppDbContext           EF Core context, 16 DbSets, model configuration
  DatabaseSeeder         Migration runner, genre seeds, admin account

Models/              ← Domain entities
  User, Media, UserLibrary, Review, Season, Episode, etc.

DTOs/                ← Request/response shapes with validation attributes

Extensions/          ← ClaimsPrincipal helpers (GetUserId, IsAdmin)

Middleware/          ← GlobalExceptionHandler (ProblemDetails)
```

### Authentication Flow

1. Client sends credentials to `POST /api/Auth/login`
2. `AuthController` verifies email exists and BCrypt hash matches
3. `JwtService` issues a signed JWT with claims: NameIdentifier, Name, Email, Role
4. Token expires after 24 hours (configurable via `Jwt:ExpiryHours`)
5. Client stores the token in `localStorage` and attaches it to every request via an Axios interceptor
6. `Program.cs` validates issuer, audience, lifetime, and HMAC-SHA256 signature with zero clock skew
7. On 401 response, the client auto-signs out via a custom event listener

### Signing Key Resolution

`JwtSettings` reads the signing key in this order:
1. Environment variable `WATCHSPHERE_JWT_KEY`
2. Configuration key `Jwt:Key`
3. Startup fails if the resolved key is missing or shorter than 32 bytes

### Catalogue Import Pipeline

```
Admin triggers search
       │
       ▼
SearchAsync() ──► Queries all configured providers in parallel
       │           Results are re-ranked by cross-source relevance
       ▼
ImportAsync() ──► Fetches full details from the chosen provider
       │           Runs deduplication (IMDb ID → provider IDs → title+year)
       │           Saves or updates the Media row
       │           Syncs episodes (add/update only, never delete)
       │           Syncs credits and people
       ▼
  Media row with seasons, episodes, cast and crew
```

### Deduplication Order

When importing a title, existing rows are checked in this sequence:

1. IMDb ID (the only cross-catalogue identifier all three providers share)
2. Each provider's own ID (TmdbId, KitsuId, TvMazeId, MalId, AniListId)
3. Hand-added row: exact title + year match on a row with no external ID
4. Cross-source row: title + year match on a row missing the incoming provider's ID

### Episode Progress Sync

The counter (`UserLibrary.EpisodesWatched`) and the episode checklist (`UserEpisode` rows) are kept in sync:

- Ticking an episode recounts the total and writes it back to the library entry
- Incrementing the counter does not alter individual episode ticks
- Completing a title (status → `Completed`) marks all episodes as watched
- Moving a title to `Planned` resets the counter to zero

Status transitions are automatic:
- Starting progress on a `Planned` title moves it to `Watching`
- Reaching the last episode moves it to `Completed` and stamps the finish date
- Movies are treated as having one episode

## Frontend Structure

```
src/
  main.js              App entry, mounts Vue with router
  router/index.js      10 lazy-loaded routes, auth guards

  api/
    client.js          Axios instance, token interceptor, 401 handler

  stores/
    auth.js            Reactive user session (loadSession, signIn, signOut)
    library.js         Reactive library state indexed by mediaId

  views/
    HomeView           Landing with hero spotlight, rails for recent/popular/airing
    BrowseView         Filterable catalogue grid with infinite scroll
    MediaDetailsView   Title detail: banner, scores, library tracker, episodes, credits, reviews
    LibraryView        Personal list with status tabs and stats
    AdminView          Full admin panel (overview, import, media CRUD, users, reviews)
    LoginView          Login form with rate-limit handling
    RegisterView       Registration form
    ProfileView        Public user profile
    PersonView         Cast/crew filmography
    NotFoundView       404 page

  components/
    AppSidebar         Navigation sidebar
    AppTopbar          Top bar with user menu
    MediaCard          Poster card with progress overlay
    MediaRail          Horizontal scrolling card strip
    HeroSpotlight      Featured title banner
    LibraryTracker     Add/update library entry widget
    EpisodeGuide       Season/episode checklist
    ReviewList         Review display with voting
    StarRating         Interactive star input
    CreditsSection     Cast and crew grid
    RelatedTitles      Franchise/related title strip
```

### Routing and Guards

The router defines three guard flags:
- `requiresAuth` — redirects unauthenticated users to `/login`
- `requiresAdmin` — redirects non-admin users to `/`
- `guestOnly` — redirects authenticated users away from login/register

`loadSession()` is called once in the `beforeEach` guard and cached so
subsequent navigations resolve immediately.

## Database Schema

16 entity types with these key relationships:

```
User ──┬── UserLibrary ──── Media ──┬── Season ── Episode
       │                            │
       ├── Review ── ReviewComment   ├── MediaGenre ── Genre
       │         └── ReviewVote      │
       └── UserEpisode ─────────────├── MediaCredit ── Person
                                    │
                                    ├── MediaRelation (self-referencing)
                                    │
                                    └── MediaStudio ── Studio
```

All relationships use cascade delete. Unique indexes enforce business rules
at the database level:

| Constraint | Columns |
|------------|---------|
| One library entry per user per title | `UserLibrary(UserId, MediaId)` |
| One review per user per title | `Review(UserId, MediaId)` |
| One episode per season slot | `Episode(SeasonId, EpisodeNumber)` |
| Unique external IDs | `Media.TmdbId`, `Media.ImdbId`, etc. |
| Unique usernames and emails | `User.Username`, `User.Email` |

## Configuration

All secrets are provided via environment variables. `appsettings.json` holds
non-secret defaults (issuer, audience, CORS origins, sync schedule).
`appsettings.Development.json` is gitignored and holds local-only overrides.

### Sync Configuration

| Key | Default | Purpose |
|-----|---------|---------|
| `Sync:Enabled` | `true` | Enable nightly catalogue refresh |
| `Sync:HourUtc` | `3` | Hour (UTC) to start the sync run |
| `Sync:MaxPerRun` | `60` | Maximum titles refreshed per run |
| `Sync:StaleAfterDays` | `14` | Days before a title is considered stale |
| `Sync:DelayMs` | `2000` | Pause between provider requests |

## API Design Conventions

- Paginated endpoints return `{ totalItems, page, pageSize, totalPages, data }`
- Error responses use the RFC 9457 ProblemDetails format
- The global exception handler maps known exception types to HTTP status codes:
  `KeyNotFoundException` → 404, `InvalidOperationException` → 409,
  `UnauthorizedAccessException` → 403, unhandled → 500
- Exception messages are included only in the `Development` environment
- Auth endpoints are rate-limited at 10 requests per minute per IP
- `pageSize` parameters are clamped to prevent abuse (typically max 100)
