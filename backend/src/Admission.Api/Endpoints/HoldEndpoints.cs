using Admission.Api.Data;
using Admission.Api.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Admission.Api.Endpoints;

public sealed record HoldSettings(int DurationSeconds);

// secondsRemaining is an addition to the spec fields: 0 whenever the status is not HELD.
public sealed record CreatedHoldResponse(
    Guid HoldId, Guid ClassId, string StudentId, string Status, DateTime ExpiresAt, int SecondsRemaining);

public sealed record HoldResponse(Guid HoldId, Guid ClassId, string Status, DateTime ExpiresAt, int SecondsRemaining);

public sealed record ConfirmedHoldResponse(Guid HoldId, string Status, int SecondsRemaining);

public sealed record MyHoldResponse(
    Guid HoldId, Guid ClassId, string ClassName, string Status, DateTime ExpiresAt, int SecondsRemaining);

public sealed record AlreadyAppliedError(string Code, string Message, Guid HoldId);

public static class HoldEndpoints
{
    public static void MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes/{classId}/holds", CreateAsync);
        app.MapGet("/holds/{holdId}", GetAsync);
        app.MapPost("/holds/{holdId}/confirm", ConfirmAsync);
        app.MapDelete("/holds/{holdId}", CancelAsync);
        app.MapGet("/me/holds", ListMineAsync);
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
            new CreatedHoldResponse(holdId, id, studentId, HoldStatus.Held, expiresAt, settings.DurationSeconds));
    }

    // No X-Student-Id needed, by the spec. A read only: a lapsed HELD hold is reported
    // as EXPIRED here; the row itself is flipped by the expiry sweeper or the next write.
    private static async Task<IResult> GetAsync(
        string holdId,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(holdId, out var id))
        {
            return HoldNotFound();
        }

        var holds = await db.Database
            .SqlQueryRaw<HoldView>(HoldViewSelect + " WHERE h.id = {0}", id)
            .ToListAsync(cancellationToken);
        if (holds.Count == 0)
        {
            return HoldNotFound();
        }

        var hold = holds[0];
        return Results.Ok(new HoldResponse(hold.HoldId, hold.ClassId, hold.Status, hold.ExpiresAt, hold.SecondsRemaining));
    }

    private static async Task<IResult> ConfirmAsync(
        string holdId,
        HttpRequest request,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        if (!StudentIdHeader.TryRead(request, out var studentId, out var error))
        {
            return error;
        }

        if (!Guid.TryParse(holdId, out var id))
        {
            return HoldNotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (status, failure) = await LockOwnHoldAsync(db, id, studentId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        IResult result;
        switch (status)
        {
            case HoldStatus.Held:
                // HELD -> CONFIRMED keeps the seat it already counts, so no seat check is needed.
                await db.Database.ExecuteSqlAsync(
                    $"UPDATE holds SET status = 'CONFIRMED' WHERE id = {id}", cancellationToken);
                result = Results.Ok(new ConfirmedHoldResponse(id, HoldStatus.Confirmed, 0));
                break;
            case HoldStatus.Confirmed:
                // A repeated confirm changes nothing, so confirmedSeats rises only once.
                result = Results.Ok(new ConfirmedHoldResponse(id, HoldStatus.Confirmed, 0));
                break;
            case HoldStatus.Expired:
                result = HoldExpired();
                break;
            default:
                result = ApiError.Result(
                    StatusCodes.Status409Conflict,
                    "HOLD_CANCELLED",
                    "This application was cancelled, so it cannot be confirmed.");
                break;
        }

        // Also keeps a HELD -> EXPIRED flip made while locking.
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<IResult> CancelAsync(
        string holdId,
        HttpRequest request,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        if (!StudentIdHeader.TryRead(request, out var studentId, out var error))
        {
            return error;
        }

        if (!Guid.TryParse(holdId, out var id))
        {
            return HoldNotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (status, failure) = await LockOwnHoldAsync(db, id, studentId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        IResult result;
        switch (status)
        {
            case HoldStatus.Held:
                // Leaving HELD frees the seat and the partial unique index at once.
                await db.Database.ExecuteSqlAsync(
                    $"UPDATE holds SET status = 'CANCELLED' WHERE id = {id}", cancellationToken);
                result = Results.NoContent();
                break;
            case HoldStatus.Cancelled:
                result = Results.NoContent();
                break;
            case HoldStatus.Confirmed:
                result = ApiError.Result(
                    StatusCodes.Status409Conflict,
                    "HOLD_CONFIRMED",
                    "This admission is already confirmed, so it cannot be cancelled.");
                break;
            default:
                result = HoldExpired();
                break;
        }

        // Also keeps a HELD -> EXPIRED flip made while locking.
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<IResult> ListMineAsync(
        HttpRequest request,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        if (!StudentIdHeader.TryRead(request, out var studentId, out var error))
        {
            return error;
        }

        var holds = await db.Database
            .SqlQueryRaw<HoldView>(
                HoldViewSelect + " WHERE h.student_id = {0} ORDER BY h.created_at DESC, h.id DESC",
                studentId)
            .ToListAsync(cancellationToken);

        return Results.Ok(holds.Select(h =>
            new MyHoldResponse(h.HoldId, h.ClassId, h.ClassName, h.Status, h.ExpiresAt, h.SecondsRemaining)));
    }

    /// <summary>
    /// Locks the class row, then the hold row, and checks ownership. A HELD hold past
    /// expires_at (by clock_timestamp()) is flipped to EXPIRED and reported as such.
    /// Must run inside a transaction.
    /// </summary>
    private static async Task<(string? Status, IResult? Failure)> LockOwnHoldAsync(
        AdmissionDbContext db,
        Guid id,
        string studentId,
        CancellationToken cancellationToken)
    {
        // class_id never changes, so it is safe to read before taking any lock.
        var classIds = await db.Database
            .SqlQuery<Guid>($"SELECT class_id AS \"Value\" FROM holds WHERE id = {id}")
            .ToListAsync(cancellationToken);
        if (classIds.Count == 0)
        {
            return (null, HoldNotFound());
        }

        // Class row first, the same lock order as applying, so confirm and cancel
        // queue with seat holds for the class and cannot deadlock with them.
        await db.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM classes WHERE id = {classIds[0]} FOR UPDATE")
            .ToListAsync(cancellationToken);

        // The hold row lock is what orders this against the expiry sweeper,
        // which does not take class locks.
        var holds = await db.Database
            .SqlQuery<LockedHold>(
                $"""
                SELECT student_id AS "StudentId", status AS "Status",
                       expires_at <= clock_timestamp() AS "Lapsed"
                FROM holds WHERE id = {id}
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
        var hold = holds[0];

        if (hold.StudentId != studentId)
        {
            return (null, ApiError.Result(
                StatusCodes.Status403Forbidden,
                "NOT_YOUR_HOLD",
                "This application belongs to another student."));
        }

        if (hold.Status == HoldStatus.Held && hold.Lapsed)
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE holds SET status = 'EXPIRED' WHERE id = {id}", cancellationToken);
            return (HoldStatus.Expired, null);
        }

        return (hold.Status, null);
    }

    // One clock reading per query, so status and secondsRemaining always agree.
    private const string HoldViewSelect =
        """
        SELECT h.id AS "HoldId",
               h.class_id AS "ClassId",
               c.name AS "ClassName",
               CASE WHEN h.status = 'HELD' AND h.expires_at <= n.ts THEN 'EXPIRED' ELSE h.status END AS "Status",
               h.expires_at AS "ExpiresAt",
               CASE WHEN h.status = 'HELD' AND h.expires_at > n.ts
                    THEN ceil(extract(epoch FROM h.expires_at - n.ts))::int
                    ELSE 0 END AS "SecondsRemaining"
        FROM holds h
        JOIN classes c ON c.id = h.class_id
        CROSS JOIN (SELECT clock_timestamp() AS ts) n
        """;

    private const string AlreadyAppliedMessage =
        "This student already has an active application or admission in this class.";

    private static IResult ClassNotFound() =>
        ApiError.Result(StatusCodes.Status404NotFound, "CLASS_NOT_FOUND", "Class not found.");

    private static IResult HoldNotFound() =>
        ApiError.Result(StatusCodes.Status404NotFound, "HOLD_NOT_FOUND", "Application not found.");

    private static IResult HoldExpired() =>
        ApiError.Result(
            StatusCodes.Status409Conflict,
            "HOLD_EXPIRED",
            "This application has expired and its seat was released.");

    private static bool IsUniqueViolation(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState
            == PostgresErrorCodes.UniqueViolation;

    internal sealed class HoldView
    {
        public Guid HoldId { get; set; }
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public int SecondsRemaining { get; set; }
    }

    internal sealed class LockedHold
    {
        public string StudentId { get; set; } = "";
        public string Status { get; set; } = "";
        public bool Lapsed { get; set; }
    }
}
