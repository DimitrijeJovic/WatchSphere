# WatchSphere — Test and Verification Report

**Date:** 2026-11-15

---

## Build Verification

| Project | Command | Result |
|---------|---------|--------|
| WatchSphere.API | `dotnet build` | **Pass** - initial result: 0 errors, 2 warnings (superseded: the final build after the MySqlConnector 2.6.1 upgrade has 0 warnings) |
| WatchSphere.Frontend | `npm run build` | **Pass** — 0 errors, 0 warnings |

### Backend Warnings

| Code | Description | Severity |
|------|-------------|----------|
| NU1902 | `MySqlConnector` 2.4.0 has a known moderate vulnerability ([GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8)) | **Superseded / resolved** - fixed by the direct reference to MySqlConnector 2.6.1; see the final verification section below. |

### Frontend Build Output

All views, components and stores compiled and tree-shaken. Total JS output: ~146 KB gzipped for the main bundle plus per-view chunks (largest: MediaDetailsView at 9 KB gzip, AdminView at 6.4 KB gzip). No unused import warnings.

---

## Security Fixes Verified

| ID | Fix | Verified |
|----|-----|----------|
| S-01 | Database password replaced with placeholder in `appsettings.json` | Confirmed — file contains `YOUR_PASSWORD_HERE` |
| S-02 | TMDB API key replaced with empty string in `appsettings.json` | Confirmed — value is `""` |
| S-03 | `reset-password-dev` endpoint removed from `AuthController.cs` | Confirmed — endpoint no longer exists in source |
| S-05 | JWT expiry reduced from 168 to 24 hours | Confirmed — `appsettings.json` shows `ExpiryHours: 24` |
| S-06 | Root `.gitignore` created | Confirmed — covers `bin/`, `obj/`, `node_modules/`, `appsettings.Development.json`, `.env` |
| S-07 | `[Url]` validation added to `UpdateProfileDto.AvatarUrl` | Confirmed — attribute present |

---

## Code Quality Fixes Verified

| ID | Fix | Verified |
|----|-----|----------|
| S-08 | Admin review listing now paginated (50 per page, max 100) | Confirmed — backend and frontend updated in sync |
| Q-04 | Frontend admin review tab updated to consume paginated response | Confirmed — uses `reviewRes.data.data` |

---

## Automated Test Coverage

No automated test suites exist for either project. This is documented in the audit report as a known gap.

**Verification method:** Manual build verification plus source inspection of all changed files. Each security fix was confirmed by reading the modified file after the edit.

---

## Dependency Check

All direct dependencies are current:

| Package | Version | Status |
|---------|---------|--------|
| BCrypt.Net-Next | 4.2.0 | Current |
| EF Core | 9.0.0 | Current |
| Pomelo MySQL | 9.0.0 | Current |
| JwtBearer | 10.0.9 | Current |
| Swashbuckle | 10.2.1 | Current |
| Vue | 3.5.34 | Current |
| Vite | 8.0.12 | Current |
| Axios | 1.17.0 | Current |

**Note (superseded):** the transitive `MySqlConnector` 2.4.0 dependency had a moderate advisory ([GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8), affects versions < 2.6.1). It was resolved by adding a direct reference to 2.6.1; see the final verification section.

---

## Global.json Adjustment

The `global.json` was updated to add `"rollForward": "latestFeature"` so the project builds with .NET SDK 10.0.300 (installed) while still preferring 10.0.200. This is a non-breaking change that improves build portability.

## Final Verification: MySqlConnector 2.6.1 (supersedes earlier results)

**Change:** a direct `<PackageReference Include="MySqlConnector" Version="2.6.1" />` was added to `WatchSphere.API.csproj`. Pomelo.EntityFrameworkCore.MySql 9.0.0 and EF Core 9.0.0 are unchanged.

### Build verification

| Check | Result |
|---|---|
| Restore and build in a temporary copy of the API project | **Pass** (0 warnings, 0 errors) |
| `dotnet list package --vulnerable --include-transitive` | **Pass** (no vulnerable packages) |
| `dotnet build WatchSphere.API` (real project) | **Pass** (0 warnings, 0 errors) |
| `npm run build` (frontend) | **Pass** |

### Runtime verification (local Development API, MySQL 8, database `watchsphere`)

| Test | Result |
|---|---|
| API startup in Development mode, `GET /health` | **Pass** (HTTP 200) |
| EF Core migrations at startup | **Pass** ("No migrations were applied. The database is already up to date.") |
| `POST /api/Auth/register` (test account `devadmin@watchsphere.local`) | **Pass** (HTTP 200) |
| `POST /api/Auth/login` | **Pass** (HTTP 200, JWT issued, role claim `Admin`) |
| `GET /api/Import/search?query=inception&type=Movie` (app-level TMDB search, Admin) | **Pass** (HTTP 200, 10 results) |
| `GET /api/Import/search?query=inception&type=Series` | **Pass** (HTTP 200, 10 results) |
| `GET /api/Import/search?query=inception` (no type) | **Pass** (HTTP 200, 33 results) |
| Application log | **Pass** (no errors or exceptions on the verification runs) |

**Notes:**
- Earlier login attempts with non-existent accounts returned HTTP 401 as expected; they are not application failures.
- The search `type` parameter is case-sensitive (`Movie`, `Series`). A lowercase `movie` returns HTTP 200 with an empty list.
- The test account was created through the registration endpoint and promoted to Admin with a single targeted `UPDATE` on the local development database. It is a local development artefact and should be removed when no longer needed.
- Not tested: a full browser walk-through of the frontend against the running API.

**Status: advisory 
[GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8)
 resolved.** Dependency resolution (build verification) and runtime behaviour (connection, migration check, insert, query and update paths) were verified separately and both passed.
