# STATUS.md — StoresPOS Implementation Status

## Project Status

- Project name: StoresPOS
- Current phase: Phase 1 complete — next is Phase 2 (Products & Categories)
- Overall progress: Phase 0 and Phase 1 verified
- Latest verified build result: Succeeded
- Last update date: 2026-11-18

## Completed Tasks

### Phase 0 — Foundation

- Solution + PL/BLL/DAL + references
- EF Core SQL Server connection
- Folder structure
- Offline Bootstrap assets
- Build verified

### Phase 1 — Custom JWT Authentication

- Custom `Users` and `Roles` tables (no ASP.NET Identity)
- `User` / `Role` entities + EF configurations + migration `InitialAuth`
- PBKDF2 password hashing (`Pbkdf2PasswordHasher`)
- Login page + Login/Logout (`AccountController`)
- JWT create/validate (`JwtTokenService`)
- JWT Bearer auth reading HttpOnly cookie `StoresPOS.AccessToken`
- Role-based authorization (`Admin`, `Manager`, `Cashier`)
- Initial Admin seed from `AdminSeed` config (Development)
- Unauthorized redirect to Login; forbidden to AccessDenied
- Controllers protected with `[Authorize]`
- Unit tests for hashing, login success/failure, expired token

## In Progress

- None

## Pending Tasks

- Phase 2 onward in `plan.md`

## Blockers

- None

## Files Created or Modified

- `plan.md`, `STATUS.md`
- DAL: `Entities/User.cs`, `Entities/Role.cs`, `Configurations/*`, `Context/ApplicationDbContext.cs`, `Migrations/*`
- BLL: auth interfaces/services/DTOs/constants/extensions
- PL: `AccountController`, Login/AccessDenied views, JWT auth extensions, `Program.cs`, appsettings
- Tests: `PasswordHasherTests`, `AuthServiceTests`

## Tests & Build Results

| Command                                 | Result                                                 |
| --------------------------------------- | ------------------------------------------------------ |
| `dotnet build StoresPOS.sln`            | Succeeded                                              |
| `dotnet test tests/StoresPOS.BLL.Tests` | Passed 5/5                                             |
| `dotnet ef database update`             | Applied `InitialAuth`; tables `Users`, `Roles` created |
| App startup seed                        | Roles Admin/Manager/Cashier + user `admin` created     |

## Next Recommended Task

Phase 2 — Products & Categories (hierarchical categories + product CRUD/pricing).

## Change Log

| Date       | Change                                                                |
| ---------- | --------------------------------------------------------------------- |
| 2026-11-17 | Phase 0 foundation verified                                           |
| 2026-11-17 | Created `plan.md` and `STATUS.md`                                     |
| 2026-11-17 | Phase 1 custom JWT auth implemented and verified                      |
| 2026-11-17 | Login UI restyled from mockups; added `theme.css` design tokens       |
| 2026-11-18 | Expanded `docs/projectOverview.md` with project purpose and structure |

## Local Dev Login (Development only)

- Username: `admin`
- Password: `Admin@12345` (from `appsettings.Development.json` — change before any real deployment)
- Run: `dotnet run --project src/StoresPOS.PL` (http://127.0.0.1:5188)
