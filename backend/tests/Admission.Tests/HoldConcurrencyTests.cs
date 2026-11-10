using static Admission.Tests.TestApi;

namespace Admission.Tests;

public sealed class HoldConcurrencyTests
{
    [Fact]
    public async Task Held_plus_confirmed_never_exceeds_total_seats_when_200_students_race_for_10_seats_across_two_api_processes()
    {
        const int students = 200;
        const int seats = 10;
        var classId = await CreateClassAsync(A, seats);
        var studentIds = NewStudentIds(students);

        // Watches the class through both processes for the whole race.
        var raceOver = new CancellationTokenSource();
        var samples = new List<(int Held, int Confirmed, int Total)>();
        var watcher = Task.Run(async () =>
        {
            for (var i = 0; !raceOver.IsCancellationRequested; i++)
            {
                var reply = await GetClassAsync(i % 2 == 0 ? A : B, classId);
                Assert.True(reply.Status == 200, $"Reading the class failed: {reply}");
                samples.Add((reply.Number("heldSeats"), reply.Number("confirmedSeats"), reply.Number("totalSeats")));
            }
        });

        // Students alternate between the two processes; each winner confirms at once,
        // so seats move from held to confirmed while the race is still on.
        var results = await RaceAsync(students, async i =>
        {
            var api = i % 2 == 0 ? A : B;
            var apply = await ApplyAsync(api, classId, studentIds[i]);
            Reply? confirm = null;
            if (apply.Status == 201)
            {
                confirm = await ConfirmAsync(i % 2 == 0 ? B : A, apply.Text("holdId"), studentIds[i]);
            }

            return (Apply: apply, Confirm: confirm);
        });

        raceOver.Cancel();
        await watcher;

        var applies = results.Select(r => r.Apply).ToList();
        var summary = Summarize(applies);
        Assert.True(applies.All(r => r.Status < 500), $"Server errors: {summary}");
        Assert.True(applies.Count(r => r.Status == 201) == seats, $"Expected {seats} x 201: {summary}");
        Assert.True(
            applies.Count(r => r.Status == 409 && r.Code == "CLASS_FULL") == students - seats,
            $"Expected {students - seats} x 409 CLASS_FULL: {summary}");
        Assert.All(results.Where(r => r.Confirm is not null), r => Assert.Equal(200, r.Confirm!.Status));

        Assert.NotEmpty(samples);
        Assert.All(samples, s => Assert.True(
            s.Held + s.Confirmed <= s.Total,
            $"Seen held {s.Held} + confirmed {s.Confirmed} > total {s.Total}"));

        var final = await GetClassAsync(B, classId);
        Assert.Equal(0, final.Number("heldSeats"));
        Assert.Equal(seats, final.Number("confirmedSeats"));
        Assert.Equal(0, final.Number("availableSeats"));
    }

    [Fact]
    public async Task A_student_sending_20_parallel_applications_for_one_class_gets_exactly_one_hold()
    {
        const int attempts = 20;
        var classId = await CreateClassAsync(A, totalSeats: 5);
        var studentId = NewStudentId();

        var replies = await RaceAsync(attempts, i => ApplyAsync(i % 2 == 0 ? A : B, classId, studentId));

        var summary = Summarize(replies);
        Assert.True(replies.All(r => r.Status < 500), $"Server errors: {summary}");
        var created = Assert.Single(replies, r => r.Status == 201);
        var refused = replies.Where(r => r.Status != 201).ToList();
        Assert.True(
            refused.Count == attempts - 1 && refused.All(r => r.Status == 409 && r.Code == "ALREADY_APPLIED"),
            $"Expected {attempts - 1} x 409 ALREADY_APPLIED: {summary}");

        // The checked path names the existing hold; only the 23505 fallback leaves it out.
        var holdId = created.Text("holdId");
        Assert.All(
            refused.Where(r => r.Body.TryGetProperty("holdId", out _)),
            r => Assert.Equal(holdId, r.Text("holdId")));

        var classReply = await GetClassAsync(A, classId);
        Assert.Equal(1, classReply.Number("heldSeats"));
    }

    [Fact]
    public async Task Twenty_parallel_confirms_of_one_hold_all_succeed_and_confirmed_seats_rise_exactly_once()
    {
        const int confirms = 20;
        var classId = await CreateClassAsync(A, totalSeats: 3);
        var studentId = NewStudentId();
        var holdId = await ApplyOkAsync(A, classId, studentId);
        var before = await GetClassAsync(A, classId);

        var replies = await RaceAsync(confirms, i => ConfirmAsync(i % 2 == 0 ? A : B, holdId, studentId));

        var summary = Summarize(replies);
        Assert.True(replies.All(r => r.Status == 200), $"Expected {confirms} x 200: {summary}");
        Assert.All(replies, r => Assert.Equal("CONFIRMED", r.Text("status")));

        var after = await GetClassAsync(B, classId);
        Assert.Equal(before.Number("confirmedSeats") + 1, after.Number("confirmedSeats"));
        Assert.Equal(before.Number("heldSeats") - 1, after.Number("heldSeats"));
        Assert.Equal(before.Number("availableSeats"), after.Number("availableSeats"));
    }
}
