# NoteService

A RESTful backend for a shared note-taking application for small teams, built on .NET 10 / ASP.NET Core Minimal APIs and EF Core (SQLite).

> Status: project scaffolding only — feature endpoints are not yet implemented.

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

The API listens on `http://localhost:5012`. In Development:

- API reference (Scalar UI): http://localhost:5012/scalar
- OpenAPI document: http://localhost:5012/openapi/v1.json
- Health check: http://localhost:5012/health

## Running tests

```bash
dotnet test
```

## Formatting

```bash
dotnet format --verify-no-changes   # check
dotnet format                       # fix
```

## Architecture

_To be documented as features are implemented._

## API examples

_To be documented as endpoints are implemented._
