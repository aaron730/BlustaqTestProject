# NoteService

A RESTful backend for a shared note-taking application for small teams, built on .NET 10 / ASP.NET Core Minimal APIs and EF Core (SQLite).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`)

## Project structure

```
src/NoteService.Api/               REST API (Endpoints, Models, Contracts, Data, Services)
tests/NoteService.UnitTests/       Unit tests (xUnit + NSubstitute)
tests/NoteService.IntegrationTests/ HTTP integration tests (xUnit + WebApplicationFactory)
.github/workflows/ci.yml           Build + test on PRs and pushes to main
```

## Running locally

```bash
dotnet restore
dotnet run --project src/NoteService.Api
```

The API listens on `http://localhost:5012`. On startup it applies EF Core migrations to a local SQLite file (`notes.db`, configured by `ConnectionStrings:Notes`), so no database setup is needed. In Development:

- API reference (Scalar UI): http://localhost:5012/scalar
- OpenAPI document: http://localhost:5012/openapi/v1.json
- Health check: http://localhost:5012/health

### Demo data

With the API running, seed sample teams and notes through the API:

```bash
./scripts/seed-demo-data.sh                              # macOS / Linux / Git Bash / WSL
BASE_URL=http://localhost:8080 ./scripts/seed-demo-data.sh
```

```powershell
./scripts/seed-demo-data.ps1                             # PowerShell 7+ or Windows PowerShell 5.1
./scripts/seed-demo-data.ps1 -BaseUrl http://localhost:8080
powershell -ExecutionPolicy Bypass -File scripts\seed-demo-data.ps1   # if script execution is disabled
```

Both scripts default to `http://localhost:5012` and create the same data.

This creates two teams (`Platform`, owned by `alice`; `Design`, owned by `dave`), team and personal notes with tags, and one edited note at version 2. Use `alice`, `bob`, `carol`, `dave`, `erin` or `mallory` (in no team) as the `X-User-Id` to see how visibility differs. Each run adds new data. To start fresh, stop the app and delete `src/NoteService.Api/notes.db`.

## Running tests

```bash
dotnet test
```

- **Unit tests** exercise the services against an in-memory SQLite database, with an NSubstitute `TimeProvider` for deterministic timestamps.
- **Integration tests** run the full HTTP pipeline through `WebApplicationFactory<Program>`. Each test class gets its own temporary SQLite file, and every test uses unique user ids.

## Formatting

```bash
dotnet format --verify-no-changes   # check
dotnet format                       # fix
```

## Architecture

```
HTTP ─▶ Endpoints/ ─▶ Services/ ─▶ Data/ (EF Core DbContext) ─▶ SQLite
         (routing,     (business rules,      Models/ (entities)
          HTTP mapping)  authorization)
```

- **Endpoints** (`Endpoints/`) are Minimal API route groups (`/api/notes`, `/api/teams`). They bind and validate requests, call a service and map its result to an HTTP response. They only ever exchange DTOs from `Contracts/`.
- **Services** (`Services/`) hold the business rules: visibility, permissions, tag normalization and optimistic concurrency. They return a `ServiceResult` (`NotFound`, `Forbidden`, `Conflict`, `PreconditionFailed`), which the endpoints turn into RFC 7807 `ProblemDetails` responses.
- **Data** (`Data/`) is the EF Core `NoteDbContext` plus migrations. Notes and teams use sequential integer IDs assigned by the database; deleted IDs are never reused. Timestamps are stored as UTC ticks so SQLite can sort and filter them.

### Authentication

The caller is identified by the `X-User-Id` header, as if the service sat behind a gateway that has already authenticated the user. A missing or blank header returns `401`. In the Scalar UI, set the header via the **Authentication** panel.

### Teams and visibility

| Note kind | Visible to | Editable by | Deletable by |
|-----------|-----------|-------------|--------------|
| Personal (`teamId` null) | Author | Author | Author |
| Team (`teamId` set) | Team members | Team members | Author, team owners |

- Creating a team makes the caller its **Owner**. Owners add and remove members. Members can leave, but a team always keeps at least one owner.
- Notes the caller cannot see return `404` (their existence isn't revealed). Acting on a team the caller isn't in returns `403`, and so does deleting a team note they aren't allowed to delete.

### Concurrency

Every note has a `version` that is also returned as an `ETag` header. `PUT` requires `If-Match`:

- Missing `If-Match`: `428 Precondition Required`.
- Stale version (someone else saved first): `412 Precondition Failed`. Re-fetch the note and retry.
- `If-Match: *`: skips the check (last write wins).

`DELETE` accepts an optional `If-Match`. When it's sent, a stale version returns `412` and the note is kept, so a delete can't remove an edit the caller hasn't seen. Without it, the delete goes ahead.

The version is also an EF Core concurrency token, so an update or delete that races another write between the check and the save is still rejected with `412`, even without `If-Match`.

## API reference

| Method | Route | Description | Success |
|--------|-------|-------------|---------|
| GET | `/api/notes` | List visible notes. Query: `teamId`, `authorId`, `search`, `tag`, `page` (default 1), `pageSize` (1–100, default 20) | 200 |
| GET | `/api/notes/{id}` | Get a note (`ETag` header) | 200 |
| POST | `/api/notes` | Create a note | 201 + `Location` |
| PUT | `/api/notes/{id}` | Replace title/content/tags (requires `If-Match`) | 200 |
| DELETE | `/api/notes/{id}` | Delete a note (optional `If-Match`) | 204 |
| GET | `/api/teams` | List the caller's teams | 200 |
| GET | `/api/teams/{id}` | Get a team with its members | 200 |
| POST | `/api/teams` | Create a team | 201 + `Location` |
| POST | `/api/teams/{id}/members` | Add a member (owners only) | 201 |
| DELETE | `/api/teams/{id}/members/{userId}` | Remove a member or leave the team | 204 |

Lists are sorted by most recently updated. `search` matches title or content case-insensitively. Tags are trimmed, lower-cased and de-duplicated (max 20 per note, 50 chars each).

## API examples

Create a team (the caller becomes owner) and add a member:

```bash
curl -X POST http://localhost:5012/api/teams \
  -H 'X-User-Id: alice' -H 'Content-Type: application/json' \
  -d '{"name":"Platform"}'
# 201 {"id":1,"name":"Platform","createdAt":"...","role":"Owner"}

curl -X POST http://localhost:5012/api/teams/1/members \
  -H 'X-User-Id: alice' -H 'Content-Type: application/json' \
  -d '{"userId":"bob"}'
```

Create a team note:

```bash
curl -i -X POST http://localhost:5012/api/notes \
  -H 'X-User-Id: alice' -H 'Content-Type: application/json' \
  -d '{"title":"Sprint planning","content":"## Goals","teamId":1,"tags":["Planning"]}'
# HTTP/1.1 201 Created
# ETag: "1"
# Location: http://localhost:5012/api/notes/1
# {"id":1,"title":"Sprint planning","content":"## Goals","authorId":"alice",
#  "teamId":1,"tags":["planning"],"createdAt":"...","updatedAt":"...","version":1}
```

Update it as another team member, passing the ETag:

```bash
curl -X PUT http://localhost:5012/api/notes/1 \
  -H 'X-User-Id: bob' -H 'If-Match: "1"' -H 'Content-Type: application/json' \
  -d '{"title":"Sprint planning (v2)","content":"## Goals\n- Ship","tags":["planning"]}'
# 200, ETag: "2"
```

Retrying with the stale ETag is rejected:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.13","title":"Precondition Failed","status":412,
 "detail":"The note has been modified since it was retrieved. Fetch the latest version and retry."}
```

Search and filter with pagination:

```bash
curl 'http://localhost:5012/api/notes?teamId=1&tag=planning&search=sprint&page=1&pageSize=10' \
  -H 'X-User-Id: bob'
# {"items":[...],"page":1,"pageSize":10,"totalCount":1,"totalPages":1}
```

Validation errors come back as `ValidationProblemDetails`:

```json
{"title":"One or more validation errors occurred.","status":400,"errors":{"Title":["The Title field is required."]}}
```

## Database migrations

The EF Core CLI is pinned as a local tool:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/NoteService.Api --output-dir Data/Migrations
```
