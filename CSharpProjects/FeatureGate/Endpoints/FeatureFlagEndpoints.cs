using System.Security.Claims;
using System.Text.Json;
using FeatureGate.Contracts;
using FeatureGate.Domain;
using FeatureGate.Persistence;
using FeatureGate.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FeatureGate.Endpoints;

public static class FeatureFlagEndpoints
{
    public const string AdminPolicy = "FeatureGateAdmin";

    private static readonly JsonSerializerOptions AuditJsonOptions =
        new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/flags", ListFlags)
            .RequireAuthorization(AdminPolicy)
            .WithName("ListFeatureFlags")
            .WithTags("Feature flags");

        app.MapGet("/api/flags/{id:guid}", GetFlag)
            .RequireAuthorization(AdminPolicy)
            .WithName("GetFeatureFlagById")
            .WithTags("Feature flags");

        app.MapGet("/api/flags/{id:guid}/audit", GetAudit)
            .RequireAuthorization(AdminPolicy)
            .WithName("GetFeatureFlagAudit")
            .WithTags("Feature flag audit");

        app.MapPost("/api/flags", CreateFlag)
            .RequireAuthorization(AdminPolicy)
            .WithName("CreateFeatureFlag")
            .WithTags("Feature flags");

        app.MapPut("/api/flags/{id:guid}", UpdateFlag)
            .RequireAuthorization(AdminPolicy)
            .WithName("UpdateFeatureFlag")
            .WithTags("Feature flags");

        app.MapDelete("/api/flags/{id:guid}", DeleteFlag)
            .RequireAuthorization(AdminPolicy)
            .WithName("DeleteFeatureFlag")
            .WithTags("Feature flags");

        app.MapPost("/api/evaluate", EvaluateFlag)
            .AllowAnonymous()
            .WithName("EvaluateFeature")
            .WithTags("Evaluation");
    }

    private static async Task<IResult> ListFlags(
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var flags = await db.FeatureFlags
            .AsNoTracking()
            .OrderBy(flag => flag.Key)
            .ToListAsync(cancellationToken);

        return Results.Ok(flags);
    }

    private static async Task<IResult> GetFlag(
        Guid id,
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var flag = await db.FeatureFlags
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return flag is null
            ? Results.NotFound(new { error = "Flag not found." })
            : Results.Ok(flag);
    }

    private static async Task<IResult> GetAudit(
        Guid id,
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var history = await db.FeatureFlagAudits
            .AsNoTracking()
            .Where(item => item.FeatureFlagId == id)
            .OrderBy(item => item.OccurredAtUtc)
            .Select(item => new FeatureFlagAuditResponse(
                item.Id,
                item.FeatureFlagId,
                item.FlagKey,
                item.Action,
                item.Actor,
                item.OccurredAtUtc,
                item.BeforeJson,
                item.AfterJson))
            .ToListAsync(cancellationToken);

        return Results.Ok(history);
    }

    private static async Task<IResult> CreateFlag(
        CreateFeatureFlagRequest request,
        ClaimsPrincipal user,
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var key = request.Key?.Trim().ToLowerInvariant();
        var description = request.Description?.Trim();
        var validationError = Validate(key, description, request.RolloutPercentage);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        if (await db.FeatureFlags.AnyAsync(flag => flag.Key == key, cancellationToken))
        {
            return Results.Conflict(new { error = "A flag with this key already exists." });
        }

        var now = DateTimeOffset.UtcNow;
        var flag = new FeatureFlag
        {
            Key = key!,
            Description = description!,
            Enabled = request.Enabled,
            RolloutPercentage = request.RolloutPercentage,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.FeatureFlags.Add(flag);
        db.FeatureFlagAudits.Add(CreateAudit(
            flag,
            "created",
            user,
            before: null,
            after: Snapshot(flag)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Results.Conflict(new { error = "A flag with this key already exists." });
        }

        return Results.CreatedAtRoute("GetFeatureFlagById", new { id = flag.Id }, flag);
    }

    private static async Task<IResult> UpdateFlag(
        Guid id,
        UpdateFeatureFlagRequest request,
        ClaimsPrincipal user,
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim();
        var validationError = ValidateDescriptionAndPercentage(
            description,
            request.RolloutPercentage);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        var flag = await db.FeatureFlags
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (flag is null)
        {
            return Results.NotFound(new { error = "Flag not found." });
        }

        var before = Snapshot(flag);
        flag.Description = description!;
        flag.Enabled = request.Enabled;
        flag.RolloutPercentage = request.RolloutPercentage;
        flag.UpdatedAtUtc = DateTimeOffset.UtcNow;

        db.FeatureFlagAudits.Add(CreateAudit(
            flag,
            "updated",
            user,
            before,
            Snapshot(flag)));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(flag);
    }

    private static async Task<IResult> DeleteFlag(
        Guid id,
        ClaimsPrincipal user,
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var flag = await db.FeatureFlags
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (flag is null)
        {
            return Results.NotFound(new { error = "Flag not found." });
        }

        var before = Snapshot(flag);
        db.FeatureFlagAudits.Add(CreateAudit(
            flag,
            "deleted",
            user,
            before,
            after: null));
        db.FeatureFlags.Remove(flag);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> EvaluateFlag(
        EvaluateFlagRequest request,
        FeatureGateDbContext db,
        CancellationToken cancellationToken)
    {
        var key = request.FlagKey?.Trim().ToLowerInvariant();
        var userId = request.UserId?.Trim();

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(userId))
        {
            return Results.BadRequest(new { error = "flagKey and userId are required." });
        }

        var flag = await db.FeatureFlags
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (flag is null)
        {
            return Results.NotFound(new { error = "Flag not found." });
        }

        return Results.Ok(new
        {
            flagKey = flag.Key,
            userId,
            enabled = flag.Enabled && FeatureFlagRolloutEvaluator.IsInRollout(
                flag.Key,
                userId,
                flag.RolloutPercentage)
        });
    }

    private static string? Validate(string? key, string? description, int percentage)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "key is required.";
        }

        if (key.Length > 100)
        {
            return "key cannot exceed 100 characters.";
        }

        return ValidateDescriptionAndPercentage(description, percentage);
    }

    private static string? ValidateDescriptionAndPercentage(
        string? description,
        int percentage)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "description is required.";
        }

        if (description.Length > 500)
        {
            return "description cannot exceed 500 characters.";
        }

        if (percentage is < 0 or > 100)
        {
            return "rolloutPercentage must be between 0 and 100.";
        }

        return null;
    }

    private static FeatureFlagSnapshot Snapshot(FeatureFlag flag) => new(
        flag.Key,
        flag.Description,
        flag.Enabled,
        flag.RolloutPercentage,
        flag.UpdatedAtUtc);

    private static FeatureFlagAudit CreateAudit(
        FeatureFlag flag,
        string action,
        ClaimsPrincipal user,
        FeatureFlagSnapshot? before,
        FeatureFlagSnapshot? after) => new()
    {
        FeatureFlagId = flag.Id,
        FlagKey = flag.Key,
        Action = action,
        Actor = user.Identity?.Name ?? "unknown",
        OccurredAtUtc = DateTimeOffset.UtcNow,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, AuditJsonOptions),
        AfterJson = after is null ? null : JsonSerializer.Serialize(after, AuditJsonOptions)
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
