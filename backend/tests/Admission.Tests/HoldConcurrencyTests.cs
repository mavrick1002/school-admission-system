using System.Net;

namespace Admission.Tests;

public sealed class HoldConcurrencyTests
{
    [Fact]
    public async Task Two_hundred_students_racing_for_ten_seats_across_two_api_processes_get_exactly_ten_holds()
    {
        const int students = 200;
        const int seats = 10;
        var apiA = TestApi.Url("API_A_URL");
        var apiB = TestApi.Url("API_B_URL");
        using var http = TestApi.CreateClient();

        var classId = await TestApi.CreateClassAsync(http, apiA, "Concurrency test", seats);

        // Every request waits on one signal, so they all hit the two processes together.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, students).Select(i => Task.Run(async () =>
        {
            await start.Task;
            var baseUrl = i % 2 == 0 ? apiA : apiB;
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/classes/{classId}/holds");
            request.Headers.Add("X-Student-Id", (30_000_000_000_000_000L + i).ToString());
            using var response = await http.SendAsync(request);
            using var body = await TestApi.ReadJsonAsync(response);
            return (Status: (int)response.StatusCode, Code: TestApi.ErrorCode(body));
        })).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(requests);

        var summary = string.Join(", ", results
            .GroupBy(r => $"{r.Status} {r.Code}".Trim())
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key} x{g.Count()}"));

        Assert.True(results.All(r => r.Status < 500), $"Server errors: {summary}");
        Assert.True(results.Count(r => r.Status == (int)HttpStatusCode.Created) == seats, $"Expected {seats} x 201: {summary}");
        Assert.True(
            results.Count(r => r.Status == (int)HttpStatusCode.Conflict && r.Code == "CLASS_FULL") == students - seats,
            $"Expected {students - seats} x 409 CLASS_FULL: {summary}");

        using var classResponse = await http.GetAsync($"{apiB}/classes/{classId}");
        using var classBody = await TestApi.ReadJsonAsync(classResponse);
        Assert.Equal(seats, classBody.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, classBody.RootElement.GetProperty("availableSeats").GetInt32());
    }
}
