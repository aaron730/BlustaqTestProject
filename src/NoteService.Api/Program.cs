using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using NoteService.Api.Auth;
using NoteService.Api.Data;
using NoteService.Api.Endpoints;
using NoteService.Api.Services;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<UserHeaderSecuritySchemeTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderAuthenticationHandler>(
        HeaderAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization();

// The connection string is resolved lazily so tests can override it through configuration.
builder.Services.AddDbContext<NoteDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("Notes")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<INotesService, NotesService>();
builder.Services.AddScoped<ITeamsService, TeamsService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Pre-select the X-User-Id scheme with a sample user so requests from the UI are authenticated.
    app.MapScalarApiReference(options => options
        .AddPreferredSecuritySchemes([HeaderAuthenticationHandler.SchemeName])
        .AddApiKeyAuthentication(HeaderAuthenticationHandler.SchemeName, apiKey => apiKey.Value = "alice"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapNoteEndpoints();
app.MapTeamEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<NoteDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

// Exposes the implicit Program class to WebApplicationFactory<Program> in integration tests.
public partial class Program;
