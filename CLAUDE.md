CLAUDE.md - Project Context & Guidelines

Project Overview

We are building a runnable RESTful backend service in .NET 10 for a shared note-taking application designed for small teams. Users need to be able to capture, organize, manage, and share notes within their teams.

Core Deliverables

Runnable .NET 10 REST API: Clean, well-structured, and easy to spin up locally.

Automated Tests: Comprehensive unit and integration test suites.a

CI/CD Pipeline: Automated workflow (e.g., GitHub Actions) that builds the solution and runs all tests on pull requests before merging to main.

README.md: Clear documentation on architecture, prerequisites, how to run the service, how to run tests, and API endpoint examples.

Tech Stack & Architecture

Framework: .NET 10.0 (net10.0) ASP.NET Core Web API (Controllers or Minimal APIs with clear route grouping).

Database / ORM: Entity Framework Core 10. Use SQLite or In-Memory database for zero-config local runnability (or PostgreSQL via Docker Compose if specified).

Authentication/Tenancy: Lightweight user/team scoping (e.g., header-based X-User-Id / X-Team-Id, JWT, or API Key for simplicity in a take-home/MVP context).

Documentation: Built-in .NET 10 OpenAPI (Microsoft.AspNetCore.OpenApi) with Scalar or Swagger UI enabled in Development.

Validation & Error Handling: ProblemDetails (RFC 7807) for standardized error responses, built-in Minimal API / DataAnnotations validation or FluentValidation for request validation.

Testing:

xUnit for test execution.

Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) for API integration tests.

Moq or NSubstitute for unit test mocking.

CI/CD: GitHub Actions (.github/workflows/ci.yml).

Solution Structure

├── .github/
│   └── workflows/
│       └── ci.yml                 # PR validation pipeline (build + test)
├── src/
│   └── NoteService.Api/           # Main REST API project (net10.0)
│       ├── Controllers/ (or Endpoints/)
│       ├── Models/                # Domain entities (Note, Team, User, Tag)
│       ├── Contracts/             # Request/Response DTOs
│       ├── Data/                  # EF Core DbContext & Migrations/Seeders
│       ├── Services/              # Business logic & data access
│       └── Program.cs             # App configuration & DI registration
├── tests/
│   ├── NoteService.UnitTests/     # Business logic unit tests (net10.0)
│   └── NoteService.IntegrationTests/ # End-to-end HTTP REST tests (net10.0)
├── NoteService.sln
├── CLAUDE.md
└── README.md


Common Commands

Build & Run

Restore dependencies: dotnet restore

Build solution: dotnet build --configuration Release

Run API locally: dotnet run --project src/NoteService.Api

Watch mode (hot reload): dotnet watch --project src/NoteService.Api

Testing

Run all tests: dotnet test

Run tests with detailed output: dotnet test --verbosity normal

Run tests in CI mode: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"

Formatting & Linting

Check code formatting: dotnet format --verify-no-changes

Fix code formatting: dotnet format

Core Domain & Features

When implementing features for the note-taking service, focus on the following capabilities for small teams:

Notes CRUD: Create, Read, Update, Delete notes (Title, Content/Markdown, CreatedAt, UpdatedAt, AuthorId, TeamId).

Team Sharing & Visibility:

Notes belong to a user and/or a team.

Support filtering notes by team, author, search query (title/content), or tags.

Concurrency / Collaboration Safety: Handle concurrent edits gracefully (e.g., optimistic concurrency via row version / ETag or UpdatedAt checks).

Pagination: Return paginated results on list endpoints (page, pageSize, totalCount).

Coding Standards & Conventions

Async/Await: All I/O bound operations (database calls, external calls) must be async and accept a CancellationToken.

DTO Separation: Never expose EF Core domain entities directly in API requests or responses. Always map to/from Request/Response DTOs (record types preferred).

HTTP Semantics:

GET returns 200 OK (or 404 Not Found).

POST returns 201 Created with a Location header pointing to the new resource.

PUT / PATCH returns 200 OK or 204 No Content.

DELETE returns 204 No Content.

Validation errors return 400 Bad Request formatted as ValidationProblemDetails.

Unauthorized/Forbidden team access returns 401 or 403.

Nullable Reference Types: Keep <Nullable>enable</Nullable> active across all projects and resolve all compiler warnings.

Testing Strategy & Requirements

Every new feature or bugfix must include automated tests:

Integration Tests (NoteService.IntegrationTests):

Use WebApplicationFactory<Program> with an isolated test database per test run.

Test the full HTTP pipeline: serialization, routing, validation, database persistence, and status codes.

Include tests for team isolation (e.g., Team A cannot modify or view private notes of Team B).

Unit Tests (NoteService.UnitTests):

Test isolated domain/service rules, validation logic, and edge cases.

Follow the Arrange-Act-Assert (AAA) pattern and name tests MethodName_Scenario_ExpectedBehavior.

CI/CD Pipeline Requirements (.github/workflows/ci.yml)

Triggers: pull_request targeting main and push to main.

Steps:

Checkout code (actions/checkout@v4).

Setup .NET 10 SDK (actions/setup-dotnet@v4 with dotnet-version: '10.0.x').

Cache NuGet packages or restore (dotnet restore).

Build with warnings as errors (dotnet build --no-restore --configuration Release -warnaserror).

Execute all unit and integration tests (dotnet test --no-build --configuration Release).