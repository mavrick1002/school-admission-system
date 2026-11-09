using Admission.Api.Data;
using Admission.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Endpoints;

public sealed record CreateClassRequest(string? Name, int? TotalSeats);

public sealed record CreatedClassResponse(Guid Id, string Name, int TotalSeats);

public sealed record ClassResponse(
    Guid Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

public static class ClassEndpoints
{
    public static void MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes", CreateAsync);
        app.MapGet("/classes", ListAsync);
        app.MapGet("/classes/{classId}", GetAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateClassRequest request,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiError.Result(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "name must not be empty.");
        }

        // PostgreSQL text cannot store a NUL character.
        if (request.Name.Contains('\0'))
        {
            return ApiError.Result(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "name contains an invalid character.");
        }

        if (request.TotalSeats is not > 0)
        {
            return ApiError.Result(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "totalSeats must be a positive whole number.");
        }

        var schoolClass = new SchoolClass
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            TotalSeats = request.TotalSeats.Value,
            CreatedAt = DateTime.UtcNow,
        };

        db.Classes.Add(schoolClass);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/classes/{schoolClass.Id}",
            new CreatedClassResponse(schoolClass.Id, schoolClass.Name, schoolClass.TotalSeats));
    }

    private static async Task<IResult> ListAsync(AdmissionDbContext db, CancellationToken cancellationToken)
    {
        var classes = await WithSeats(db.Classes.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id))
            .ToListAsync(cancellationToken);

        return Results.Ok(classes.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        string classId,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(classId, out var id))
        {
            var schoolClass = await WithSeats(db.Classes.Where(c => c.Id == id)).SingleOrDefaultAsync(cancellationToken);
            if (schoolClass is not null)
            {
                return Results.Ok(ToResponse(schoolClass));
            }
        }

        return ApiError.Result(StatusCodes.Status404NotFound, "CLASS_NOT_FOUND", "Class not found.");
    }

    // Counts come from the holds table in one statement, so each row is a consistent
    // snapshot. DateTime.UtcNow translates to the database clock, shared by every process.
    // Filter and sort before calling this: EF cannot translate either over this projection.
    private static IQueryable<SeatCounts> WithSeats(IQueryable<SchoolClass> classes) =>
        classes.AsNoTracking().Select(c => new SeatCounts(
            c.Id,
            c.Name,
            c.TotalSeats,
            c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > DateTime.UtcNow),
            c.Holds.Count(h => h.Status == HoldStatus.Confirmed)));

    private static ClassResponse ToResponse(SeatCounts c) => new(
        c.Id,
        c.Name,
        c.TotalSeats,
        c.Held,
        c.Confirmed,
        c.TotalSeats - c.Held - c.Confirmed);

    private sealed record SeatCounts(Guid Id, string Name, int TotalSeats, int Held, int Confirmed);
}
