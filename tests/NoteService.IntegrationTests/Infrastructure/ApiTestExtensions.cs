using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using NoteService.Api.Contracts;

namespace NoteService.IntegrationTests.Infrastructure;

public static class ApiTestExtensions
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string NewUserId() => $"user-{Guid.NewGuid():N}";

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value;
    }

    public static async Task<TeamResponse> CreateTeamAsync(this HttpClient client, string name = "Team")
    {
        var response = await client.PostAsJsonAsync("/api/teams", new CreateTeamRequest(name));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<TeamResponse>();
    }

    public static async Task AddMemberAsync(this HttpClient client, int teamId, string userId)
    {
        var response = await client.PostAsJsonAsync($"/api/teams/{teamId}/members", new AddTeamMemberRequest(userId));
        response.EnsureSuccessStatusCode();
    }

    public static async Task<NoteResponse> CreateNoteAsync(
        this HttpClient client, string title = "Title", string? content = "Content", int? teamId = null,
        IReadOnlyList<string>? tags = null)
    {
        var response = await client.PostAsJsonAsync("/api/notes", new CreateNoteRequest(title, content, teamId, tags));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<NoteResponse>();
    }

    public static Task<HttpResponseMessage> PutNoteAsync(
        this HttpClient client, int noteId, UpdateNoteRequest request, string? ifMatch)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/notes/{noteId}")
        {
            Content = JsonContent.Create(request),
        };

        if (ifMatch is not null)
        {
            message.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(message);
    }
}
