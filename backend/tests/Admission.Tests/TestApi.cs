using System.Net.Http.Json;
using System.Text.Json;

namespace Admission.Tests;

/// <summary>
/// Talks to running API processes over real HTTP. The URLs come from the
/// "test" compose profile, so these tests never touch the default database.
/// </summary>
internal static class TestApi
{
    public static string Url(string variable) =>
        Environment.GetEnvironmentVariable(variable)?.TrimEnd('/')
        ?? throw new InvalidOperationException(
            $"{variable} is not set. Run the tests with: docker compose --profile test run --rm --build backend-tests");

    public static HttpClient CreateClient() =>
        new(new SocketsHttpHandler { MaxConnectionsPerServer = int.MaxValue })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

    public static async Task<string> CreateClassAsync(HttpClient http, string baseUrl, string name, int totalSeats)
    {
        using var response = await http.PostAsJsonAsync($"{baseUrl}/classes", new { name, totalSeats });
        response.EnsureSuccessStatusCode();
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public static string? ErrorCode(JsonDocument body) =>
        body.RootElement.ValueKind == JsonValueKind.Object
        && body.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
}
