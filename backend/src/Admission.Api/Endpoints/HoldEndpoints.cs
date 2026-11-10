using Admission.Api.Data;
using Admission.Api.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Admission.Api.Endpoints;

public sealed record HoldSettings(int DurationSeconds);

public sealed record CreatedHoldResponse(Guid HoldId, Guid ClassId, string StudentId, string Status, DateTime ExpiresAt);

public sealed record AlreadyAppliedError(string Code, string Message, Guid HoldId);

public static class HoldEndpoints
{
    public static void MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes/{classId}/holds", CreateAsync);
    }

    private static async Task<IResult> CreateAsync(
        string classId,
        HttpRequest request,
        AdmissionDbContext db,
        HoldSettings settings,
        CancellationToken cancellationToken)
    {
        if (!StudentIdHeader.TryRead(request, out var studentId, out var error))
        {
            return error;
        }

        if (!Guid.TryParse(classId, out var id))
        {
            return ClassNotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Every hold request for this class queues on the class row, so the count
        // below cannot go stale before the insert, across any number of API processes.
        var totalSeats = await db.Database
            .SqlQuery<int>($"SELECT total_seats AS \"Value\" FROM classes WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (totalSeats.Count == 0)
        {
            return ClassNotFound();
        }

        // Also frees the partial unique index for students whose earlier hold lapsed.
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE holds SET status = 'EXPIRED'
            WHERE class_id = {id} AND status = 'HELD' AND expires_at <= clock_timestamp()
            """,
            cancellationToken);

        // Checked before the seat count, so a student who already holds a seat
        // hears that, and gets its id, even when the class is full.
        var existingHoldIds = await db.Database
            .SqlQuery<Guid>(
                $"""
                SELECT id AS "Value" FROM holds
                WHERE class_id = {id} AND student_id = {studentId} AND status IN ('HELD', 'CONFIRMED')
                LIMIT 1
                """)
            .ToListAsync(cancellationToken);
        if (existingHoldIds.Count > 0)
        {
            // Keep the expiry flips; there is nothing else to undo.
            await transaction.CommitAsync(cancellationToken);
            return Results.Json(
                new AlreadyAppliedError("ALREADY_APPLIED", AlreadyAppliedMessage, existingHoldIds[0]),
                statusCode: StatusCodes.Status409Conflict);
        }

        var activeHolds = await db.Database
            .SqlQuery<int>(
                $"""
                SELECT count(*)::int AS "Value" FROM holds
                WHERE class_id = {id} AND status IN ('HELD', 'CONFIRMED')
                """)
            .ToListAsync(cancellationToken);
        if (activeHolds[0] >= totalSeats[0])
        {
            // Keep the expiry flips; there is nothing else to undo.
            await transaction.CommitAsync(cancellationToken);
            return ApiError.Result(
                StatusCodes.Status409Conflict,
                "CLASS_FULL",
                "There are no seats left in this class.");
        }

        var holdId = Guid.CreateVersion7();
        DateTime expiresAt;
        try
        {
            var inserted = await db.Database
                .SqlQuery<DateTime>(
                    $"""
                    INSERT INTO holds (id, class_id, student_id, status, expires_at, created_at)
                    VALUES ({holdId}, {id}, {studentId}, 'HELD',
                            clock_timestamp() + make_interval(secs => {settings.DurationSeconds}),
                            clock_timestamp())
                    RETURNING expires_at AS "Value"
                    """)
                .ToListAsync(cancellationToken);
            expiresAt = inserted[0];
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Fallback only: the check above normally answers first.
            return ApiError.Result(StatusCodes.Status409Conflict, "ALREADY_APPLIED", AlreadyAppliedMessage);
        }

        return Results.Created(
            $"/holds/{holdId}",
            new CreatedHoldResponse(holdId, id, studentId, HoldStatus.Held, expiresAt));
    }

    private const string AlreadyAppliedMessage =
        "This student already has an active application or admission in this class.";

    private static IResult ClassNotFound() =>
        ApiError.Result(StatusCodes.Status404NotFound, "CLASS_NOT_FOUND", "Class not found.");

    private static bool IsUniqueViolation(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState
            == PostgresErrorCodes.UniqueViolation;
}
