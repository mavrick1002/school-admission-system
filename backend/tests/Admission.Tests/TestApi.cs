using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Admission.Tests;

/// <summary>An HTTP response reduced to what the tests assert on.</summary>
internal sealed record Reply(int Status, JsonElement Body)
{
    public string? Code =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;

    public string Text(string name) => Body.GetProperty(name).GetString()!;

    public int Number(string name) => Body.GetProperty(name).GetInt32();

    public override string ToString() => $"{Status} {(Body.ValueKind == JsonValueKind.Undefined ? "" : Body.GetRawText())}";
}

/// <summary>
/// Talks to running API processes over real HTTP. The URLs come from the
/// "test" compose profile, so these tests never touch the default database.
/// </summary>
internal static class TestApi
{
    /// <summary>API process A, 60 s holds.</summary>
    public static string A => Url("API_A_URL");

    /// <summary>API process B, 60 s holds, same database as A.</summary>
    public static string B => Url("API_B_URL");

    /// <summary>API process with 2 s holds, same database as A and B.</summary>
    public static string Short => Url("API_SHORT_URL");

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = int.MaxValue,
        // Lets a test send non-ASCII header values, such as Bengali digits.
        RequestHeaderEncodingSelector = (_, _) => Encoding.UTF8,
    })
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private static string Url(string variable) =>
        Environment.GetEnvironmentVariable(variable)?.TrimEnd('/')
        ?? throw new InvalidOperationException(
            $"{variable} is not set. Run the tests with: docker compose --profile test run --rm --build backend-tests");

    /// <summary>A random, valid 17-digit birth registration number.</summary>
    public static string NewStudentId() =>
        "4" + Random.Shared.NextInt64(0, 10_000_000_000_000_000L).ToString("D16");

    public static string[] NewStudentIds(int count)
    {
        var ids = new HashSet<string>();
        while (ids.Count < count)
        {
            ids.Add(NewStudentId());
        }

        return [.. ids];
    }

    public static async Task<Reply> SendAsync(
        HttpMethod method, string url, string? studentId = null, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (studentId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        }

        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var body = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        return new Reply((int)response.StatusCode, body);
    }

    public static async Task<string> CreateClassAsync(string baseUrl, int totalSeats)
    {
        var reply = await SendAsync(
            HttpMethod.Post,
            $"{baseUrl}/classes",
            content: JsonContent.Create(new { name = $"Test class {Guid.NewGuid():N}", totalSeats }));
        Assert.True(reply.Status == 201, $"Creating a class failed: {reply}");
        return reply.Text("id");
    }

    public static Task<Reply> GetClassAsync(string baseUrl, string classId) =>
        SendAsync(HttpMethod.Get, $"{baseUrl}/classes/{classId}");

    public static Task<Reply> ApplyAsync(string baseUrl, string classId, string studentId) =>
        SendAsync(HttpMethod.Post, $"{baseUrl}/classes/{classId}/holds", studentId);

    public static async Task<string> ApplyOkAsync(string baseUrl, string classId, string studentId)
    {
        var reply = await ApplyAsync(baseUrl, classId, studentId);
        Assert.True(reply.Status == 201, $"Applying failed: {reply}");
        return reply.Text("holdId");
    }

    public static Task<Reply> GetHoldAsync(string baseUrl, string holdId) =>
        SendAsync(HttpMethod.Get, $"{baseUrl}/holds/{holdId}");

    public static Task<Reply> ConfirmAsync(string baseUrl, string holdId, string studentId) =>
        SendAsync(HttpMethod.Post, $"{baseUrl}/holds/{holdId}/confirm", studentId);

    public static Task<Reply> CancelAsync(string baseUrl, string holdId, string studentId) =>
        SendAsync(HttpMethod.Delete, $"{baseUrl}/holds/{holdId}", studentId);

    /// <summary>
    /// Starts every call at once: each waits on one signal, so they reach the API together.
    /// </summary>
    public static async Task<T[]> RaceAsync<T>(int count, Func<int, Task<T>> call)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = Enumerable.Range(0, count).Select(i => Task.Run(async () =>
        {
            await start.Task;
            return await call(i);
        })).ToArray();

        start.SetResult();
        return await Task.WhenAll(calls);
    }

    /// <summary>"201 x10, 409 CLASS_FULL x190", for assertion messages.</summary>
    public static string Summarize(IEnumerable<Reply> replies) =>
        string.Join(", ", replies
            .GroupBy(r => $"{r.Status} {r.Code}".Trim())
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key} x{g.Count()}"));
}
