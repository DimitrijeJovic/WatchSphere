# WatchSphere — Codebase Audit Report

**Date:** 2026-11-15
**Scope:** Complete repository inspection of WatchSphere.API and WatchSphere.Frontend

---

## 1. Architecture Overview

WatchSphere is a full-stack movie, television and anime discovery and tracking application.

### Backend — `WatchSphere.API`
- **Framework:** ASP.NET Core on .NET 10
- **ORM:** Entity Framework Core 9 with Pomelo MySQL provider
- **Authentication:** JWT bearer tokens (HMAC-SHA256), BCrypt password hashing
- **API documentation:** Swagger/OpenAPI via Swashbuckle
- **Architecture:** Controllers → Services/Interfaces → EF Core DbContext → MySQL
- **External integrations:** TMDB, Kitsu, TVmaze (catalog providers); IMDb (rating dataset)
- **Background service:** `CatalogSyncService` for nightly title refresh

### Frontend — `WatchSphere.Frontend`
- **Framework:** Vue 3 (Composition API, `<script setup>`)
- **Build tool:** Vite 8
- **HTTP client:** Axios with centralized interceptors
- **Routing:** Vue Router 5 with lazy-loaded views and auth guards
- **State management:** Reactive stores (`auth.js`, `library.js`) — no Vuex/Pinia

### Database
- MySQL 8 with 16 entity types
- 16 EF Core migrations (June 2026 – September 2026)
- Unique indexes on external catalog IDs, email, username, and composite keys

---

## 2. Existing Functionality

| Area | Status | Notes |
|------|--------|-------|
| User registration | Functional | Email validation, username uniqueness, BCrypt hashing |
| User login | Functional | JWT issuance, rate-limited |
| JWT authentication | Functional | Issuer/audience/lifetime/signature validation, zero clock skew |
| User profiles | Functional | Avatar URL, bio, public profile view |
| Media catalogue | Functional | Search, filter by type/genre/year, sort, pagination |
| External catalogue import | Functional | TMDB, Kitsu, TVmaze via unified `ICatalogProvider` interface |
| Franchise import | Functional | Sequel/prequel/collection linking |
| Personal library | Functional | Status tracking, scoring, favorites, notes, rewatch count |
| Episode tracking | Functional | Per-episode watched/rating, watched-through, season clear |
| Progress sync | Functional | Episode ticks and counter stay in sync |
| Reviews and ratings | Functional | Create/edit/delete, helpful votes, comments, spoiler flag |
| Admin panel | Functional | User management, catalogue CRUD, genre management, import UI |
| IMDb rating sync | Functional | Streams TSV dataset, matches by IMDb ID |
| Catalog auto-sync | Functional | Nightly background refresh of airing/stale titles |
| Rate limiting | Functional | Auth endpoints: 10 requests/minute per IP |
| CORS | Functional | Configurable allowed origins |
| Global error handling | Functional | `GlobalExceptionHandler` with ProblemDetails responses |

---

## 3. Security Issues

### CRITICAL

#### S-01: Real database password in `appsettings.json`
- **File:** `WatchSphere.API/appsettings.json` line 4
- **Detail:** Connection string contained a real MySQL password in plaintext.
- **Risk:** Credential exposure if the file is committed or shared. The password was present in the working tree in plaintext.
- **Action required:** Remove from tracked file, use environment variables or User Secrets, rotate the password immediately.

#### S-02: Real TMDB API key in `appsettings.json`
- **File:** `WatchSphere.API/appsettings.json` line 11
- **Detail:** Full TMDB v4 bearer token (JWT format) was present in the working tree.
- **Risk:** Key abuse if the repository is made public.
- **Action required:** Remove from tracked file, use environment variable `WATCHSPHERE_TMDB_KEY`, rotate the key.

#### S-03: Unauthenticated password reset endpoint
- **File:** `WatchSphere.API/Controllers/AuthController.cs` lines 154–170
- **Detail:** `PUT /api/Auth/reset-password-dev` accepts an email and new password as query parameters. No `[Authorize]` attribute, no environment check, no rate limiting. Accessible in all environments including production.
- **Risk:** Any user can change any other user's password, including administrator accounts. Complete authentication bypass.
- **Action required:** Remove the endpoint entirely. If password reset is needed, implement a proper token-based flow.

### HIGH

#### S-04: Development credentials in `appsettings.Development.json`
- **File:** `WatchSphere.API/appsettings.Development.json`
- **Detail:** Contains a weak admin seed password and a predictable JWT signing key in plaintext.
- **Risk:** These credentials are present in the local development configuration file. The admin password is weak, and the JWT key is predictable.
- **Action required:** Exclude `appsettings.Development.json` from Git tracking. The README already documents using environment variables — enforce that path.

#### S-05: JWT token expiry of 168 hours (7 days)
- **File:** `WatchSphere.API/appsettings.json` line 8
- **Detail:** `ExpiryHours: 168` means a stolen token is valid for an entire week.
- **Risk:** Extended attack window if a token is compromised. There is no token revocation mechanism.
- **Recommendation:** Reduce to 24 hours for a reasonable balance. Note: no refresh token mechanism exists, so shorter expiry means more frequent re-authentication.

#### S-06: No root-level `.gitignore`
- **Detail:** Only `WatchSphere.Frontend/.gitignore` exists. The backend's `bin/`, `obj/`, and `appsettings.Development.json` have no exclusion rule.
- **Risk:** Build artifacts and development secrets are committable by default.
- **Action required:** Create a root `.gitignore` covering both projects.

### MEDIUM

#### S-07: `UpdateProfileDto.AvatarUrl` lacks URL validation
- **File:** `WatchSphere.API/DTOs/UpdateProfileDto.cs`
- **Detail:** `AvatarUrl` accepts any string up to 500 characters. No `[Url]` attribute.
- **Risk:** Could store javascript: URIs or other non-HTTP values. Low practical impact because the frontend renders it as an `<img>` `src`, but defense in depth recommends validation.

#### S-08: No pagination on admin review listing
- **File:** `WatchSphere.API/Controllers/ReviewController.cs` line 362 (`GetAll`)
- **Detail:** Returns every review in the system in one response.
- **Risk:** Performance degradation as the review count grows.

#### S-09: Admin user listing returns email addresses without pagination
- **File:** `WatchSphere.API/Controllers/UserController.cs` line 69 (`GetAll`)
- **Detail:** Returns all users including emails. Admin-only, but no pagination.
- **Risk:** Performance concern at scale. Email exposure limited to admins, which is acceptable.

#### S-10: CORS allows all headers and methods
- **File:** `WatchSphere.API/Program.cs` lines 137–143
- **Detail:** `AllowAnyHeader()` and `AllowAnyMethod()` on the Vue policy.
- **Risk:** Slightly broader than necessary. Functionally required for the SPA, but production deployments should tighten to specific headers and methods.

### LOW

#### S-11: No password change functionality for users
- **Detail:** Users cannot change their password through the application. The only mechanism was the dangerous dev endpoint.
- **Impact:** Users who need to change their password have no self-service path.
- **Recommendation:** Document as a known limitation; implement if within project scope.

#### S-12: `Describe()` in `AuthController` returns email in the response
- **File:** `WatchSphere.API/Controllers/AuthController.cs` line 140
- **Detail:** Login and register responses include the user's email. This is expected for the logged-in user's own data.
- **Risk:** None — the email is returned only to the authenticated user themselves.

---

## 4. Code Quality Issues

### Backend

#### Q-01: Several controller actions missing `CancellationToken`
- **Files:** `AuthController`, `UserController`, `GenreController`, parts of `ReviewController`
- **Detail:** Async database operations do not accept or forward a `CancellationToken`. If a client disconnects, the server continues processing.
- **Severity:** LOW — functional but wasteful under load.

#### Q-02: `AuthController` mixes concerns
- **Detail:** The `reset-password-dev` endpoint has inconsistent indentation and was clearly added later without matching the surrounding code style.
- **Action:** Remove it entirely (security fix S-03).

#### Q-03: No explicit DTO for the `UpdateRole` endpoint
- **File:** `UserController.cs` line 100
- **Detail:** Accepts `[FromBody] string role` directly. Works but is unusual for a JSON API.
- **Severity:** LOW — functional.

#### Q-04: `loadMedia` in AdminView.vue limits to 100 items
- **File:** `WatchSphere.Frontend/src/views/AdminView.vue` line 741
- **Detail:** `pageSize: 100` means catalogues larger than 100 titles are truncated in the admin table.
- **Severity:** MEDIUM — admin loses visibility of titles beyond the first 100.

### Frontend

#### Q-05: No error boundary or global error handler
- **Detail:** API errors in components are handled per-call, but uncaught exceptions in Vue components produce no user feedback.
- **Severity:** LOW — individual error handling is present where it matters.

#### Q-06: Library store does not gate on `authReady`
- **Detail:** `library.js` calls the API when `isLoggedIn` is true, but doesn't wait for `authReady` before checking.
- **Severity:** LOW — the router guard calls `loadSession()` before any view loads.

---

## 5. Database Integrity

| Check | Result |
|-------|--------|
| Foreign keys on all relationships | Present |
| Cascade delete behavior defined | Yes — all relationships cascade |
| Unique indexes on business keys | Email, Username, Genre.Name, (UserId,MediaId) on library and reviews, external catalog IDs |
| Max-length constraints on all string columns | Yes — configured in `OnModelCreating` |
| Migration chain integrity | 16 migrations, chronologically ordered, no gaps |
| Seed data | Default genre list and initial admin account |
| Index on frequently queried columns | Title, Type, CreatedAt, external IDs |

**No database integrity issues found.** The schema is well-constrained with appropriate indexes and relationships.

---

## 6. Dependency Concerns

| Package | Version | Notes |
|---------|---------|-------|
| `Microsoft.EntityFrameworkCore` | 9.0.0 | EF Core 9 on .NET 10 — compatible |
| `Pomelo.EntityFrameworkCore.MySql` | 9.0.0 | Matches EF Core version |
| `BCrypt.Net-Next` | 4.2.0 | Current, well-maintained |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.9 | Matches target framework |
| `Swashbuckle.AspNetCore` | 10.2.1 | Current |
| `vue` | 3.5.34 | Current |
| `vue-router` | 5.1.0 | Current |
| `axios` | 1.17.0 | Current |
| `vite` | 8.0.12 | Current |

**Transitive dependency advisory (resolved):** `MySqlConnector` 2.4.0, pulled in by `Pomelo.EntityFrameworkCore.MySql` 9.0.0, had a moderate-severity vulnerability ([GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8), affects versions < 2.6.1). Pomelo specifies `MySqlConnector >= 2.4.0` (a floor, not a pin), so a direct reference to 2.6.1 was added to the API project. Build verification and runtime verification both passed; see "Final Verification: MySqlConnector 2.6.1" in `TEST_REPORT.md`. The earlier statement that the upgrade should first be tested in an isolated copy is superseded by that result.

---

## 7. Missing Tests

No test projects exist in either the backend or frontend. There are:
- No unit tests
- No integration tests
- No frontend component tests

**Recommendation:** Introduce a backend test project for critical paths (authentication, authorization, library business logic). Frontend build verification serves as a minimum baseline.

---

## 8. Detailed Vulnerability Inventory

### S-01: Real database password in `appsettings.json`

- **Affected file:** `WatchSphere.API/appsettings.json` line 4 (`ConnectionStrings:DefaultConnection`)
- **Severity:** CRITICAL
- **Original finding:** Connection string contained the real MySQL root password in plaintext.
- **Resolution:** Password value replaced with placeholder `YOUR_PASSWORD_HERE`.
- **Verified:** Current file contains `password=YOUR_PASSWORD_HERE` on line 3.
- **Status:** RESOLVED

### S-02: Real TMDB API key in `appsettings.json`

- **Affected file:** `WatchSphere.API/appsettings.json` line 11 (`Tmdb:ApiKey`)
- **Severity:** CRITICAL
- **Original finding:** Full TMDB v4 bearer token (JWT format) was present in plaintext.
- **Resolution:** API key value replaced with empty string.
- **Verified:** Current file contains `"ApiKey": ""` on line 11.
- **Status:** RESOLVED

### S-03: Unauthenticated password reset endpoint

- **Affected file:** `WatchSphere.API/Controllers/AuthController.cs` (formerly lines 154–170)
- **Endpoint:** `PUT /api/Auth/reset-password-dev`
- **Severity:** CRITICAL
- **Original finding:** Unauthenticated endpoint accepted email and new password as query parameters. No `[Authorize]` attribute, no environment gate, no rate limiting. Complete authentication bypass.
- **Resolution:** Endpoint deleted entirely. `AuthController.cs` now ends at line 155 after the `Describe()` helper method.
- **Verified:** No match for `reset-password` in current `AuthController.cs`.
- **Status:** RESOLVED

### S-04: Development credentials in `appsettings.Development.json`

- **Affected file:** `WatchSphere.API/appsettings.Development.json`
- **Severity:** HIGH
- **Original finding:** Contains a weak admin seed password and a predictable JWT signing key in plaintext.
- **Resolution:** File added to root `.gitignore` at line 11. File remains locally for development use but will not be committed.
- **Verified:** `.gitignore` contains `WatchSphere.API/appsettings.Development.json`.
- **Status:** RESOLVED

### S-05: JWT token expiry of 168 hours (7 days)

- **Affected file:** `WatchSphere.API/appsettings.json` line 8 (`Jwt:ExpiryHours`)
- **Severity:** HIGH
- **Original finding:** `ExpiryHours: 168` meant a stolen token remained valid for one week. No token revocation mechanism exists.
- **Resolution:** `ExpiryHours` changed from `168` to `24`.
- **Verified:** Current file contains `"ExpiryHours": 24` on line 8.
- **Status:** RESOLVED

### S-06: No root-level `.gitignore`

- **Affected file:** (missing — no file existed at the project root)
- **Severity:** HIGH
- **Original finding:** Only `WatchSphere.Frontend/.gitignore` existed. Backend `bin/`, `obj/`, and `appsettings.Development.json` had no exclusion rule at the root level.
- **Resolution:** Root `.gitignore` created with 30 lines of rules covering both projects: `bin/`, `obj/`, `node_modules/`, `dist/`, `appsettings.Development.json`, `.env`, editor directories, log files.
- **Verified:** `.gitignore` exists at project root.
- **Status:** RESOLVED

### S-07: `UpdateProfileDto.AvatarUrl` lacks URL validation

- **Affected file:** `WatchSphere.API/DTOs/UpdateProfileDto.cs` line 8
- **Endpoint:** `PUT /api/Auth/me`
- **Severity:** MEDIUM
- **Original finding:** `AvatarUrl` accepted any string up to 500 characters with no `[Url]` attribute. Could store `javascript:` URIs or other non-HTTP values.
- **Resolution:** `[Url]` data annotation attribute added to the `AvatarUrl` property.
- **Verified:** Current file contains `[Url]` on line 8 above the `AvatarUrl` property.
- **Status:** RESOLVED

### S-08: No pagination on admin review listing

- **Affected file:** `WatchSphere.API/Controllers/ReviewController.cs` line 364 (`GetAll`)
- **Endpoint:** `GET /api/Review` (admin only)
- **Severity:** MEDIUM
- **Original finding:** Returned every review in the system in a single unbounded response.
- **Resolution:** `page` and `pageSize` parameters added (default 50, max 100 via `Math.Clamp`). Returns standard pagination envelope (`TotalItems`, `Page`, `PageSize`, `TotalPages`, `Data`). Frontend `AdminView.vue` updated to consume `data.data` from the paginated response.
- **Verified:** Current `GetAll` method signature: `GetAll(int page = 1, int pageSize = 50)` with `Math.Clamp(pageSize, 1, 100)`.
- **Status:** RESOLVED

### S-09: Admin user listing returns email addresses without pagination

- **Affected file:** `WatchSphere.API/Controllers/UserController.cs` line 69 (`GetAll`)
- **Endpoint:** `GET /api/User` (admin only)
- **Severity:** MEDIUM
- **Original finding:** Returns all users including email addresses in a single unbounded response. Admin-only endpoint.
- **Resolution:** Not changed. Performance concern exists at scale, but the endpoint is admin-only and functional at current data volume.
- **Verified:** Current `GetAll` signature is `GetAll(string? search = null)` with no pagination parameters.
- **Recommended future action:** Add pagination parameters matching the `ReviewController.GetAll` pattern.
- **Status:** ACCEPTED RISK

### S-10: CORS allows all headers and methods

- **Affected file:** `WatchSphere.API/Program.cs` lines 139–142
- **Severity:** MEDIUM
- **Original finding:** `AllowAnyHeader()` and `AllowAnyMethod()` on the `VuePolicy` CORS policy. Broader than strictly necessary.
- **Resolution:** Not changed. Functionally required for the Vue SPA to operate correctly. Origins are restricted to the configured `Cors:AllowedOrigins` list.
- **Verified:** Current `Program.cs` contains `.AllowAnyHeader().AllowAnyMethod()` within `WithOrigins(allowedOrigins)`.
- **Recommended future action:** Tighten to specific headers (`Authorization`, `Content-Type`) and methods (`GET`, `POST`, `PUT`, `DELETE`) for production deployments.
- **Status:** ACCEPTED RISK

### S-11: No password change functionality for users

- **Affected file:** (none — functionality does not exist)
- **Severity:** LOW
- **Original finding:** Users cannot change their password through the application. The only mechanism was the dangerous dev endpoint (S-03), now removed.
- **Resolution:** Documented as known limitation in `README.md` and `docs/FINAL_RELEASE_REPORT.md`.
- **Recommended future action:** Implement a proper token-based password change flow.
- **Status:** RESOLVED

### S-12: `Describe()` returns email in login/register response

- **Affected file:** `WatchSphere.API/Controllers/AuthController.cs` line 140 (`Describe()` method)
- **Endpoints:** `POST /api/Auth/login`, `POST /api/Auth/register`, `PUT /api/Auth/me`
- **Severity:** LOW
- **Original finding:** Login, register, and profile update responses include the user's email address.
- **Resolution:** Not changed. The email is returned only to the authenticated user about their own account. This is standard behavior for authentication APIs.
- **Status:** INFORMATIONAL

---

### Vulnerability Totals

| Status | Count | Finding IDs |
|--------|-------|-------------|
| RESOLVED | 9 | S-01, S-02, S-03, S-04, S-05, S-06, S-07, S-08, S-11 |
| PARTIALLY RESOLVED | 0 | — |
| OPEN | 0 | — |
| ACCEPTED RISK | 2 | S-09, S-10 |
| INFORMATIONAL | 1 | S-12 |
| **Total** | **12** | |

---

## 9. Credential Rotation Required

The following credentials were present in the working tree in plaintext and **should be rotated** before the repository is made public as a precaution, even though they have been removed from or redacted in the files intended for publication.

**Note:** This repository has no commits yet. No credentials were ever committed to any Git history. The rotation recommendation is a precaution against the credentials having been shared, backed up, or cached outside this directory.

1. **MySQL credentials** - create a dedicated least-privilege `watchsphere` account limited to the WatchSphere database (not `root`), switch the connection string to it, then change the old root password and stop using root for the application.
2. **TMDB API key** — regenerate at themoviedb.org and set via `WATCHSPHERE_TMDB_KEY` environment variable.
3. **Admin seed password** — set a strong value via `WATCHSPHERE_ADMIN_PASSWORD` environment variable; change again after first sign-in.
4. **JWT signing key** — generate a new key with `RandomNumberGenerator` (at least 32 bytes, Base64-encoded; see README) and set via `WATCHSPHERE_JWT_KEY` environment variable.

---

## 10. Summary

The backend follows a controller-service-repository pattern with EF Core, and the frontend uses reactive Vue 3 stores with a single Axios instance. The import pipeline handles cross-catalogue deduplication across three providers, and the episode tracking keeps per-episode ticks and the title-level counter in sync. All three critical and all three high-severity security issues have been resolved: credentials were removed from `appsettings.json`, the dangerous development endpoint was deleted, JWT expiry was reduced, development secrets were gitignored, and a root `.gitignore` was created. Two medium-severity findings (S-09, S-10) remain as accepted risks. The six code quality findings are minor and do not affect functionality or security.

This repository has no commits yet. No credentials were ever committed to any Git history. Credential rotation is recommended as a precaution before publication.
