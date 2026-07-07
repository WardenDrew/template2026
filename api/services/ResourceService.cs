using Microsoft.EntityFrameworkCore;
using NodaTime;
using TemplateApi.Data;
using TemplateApi.Models;

namespace TemplateApi.Services;

public sealed class ResourceService(
    AppDbContext dbContext,
    IClock clock,
    AuthorizationService authorizationService
)
{
    public async Task<IReadOnlyList<ResourceDto>> ListCurrentUserResourcesAsync(
        HttpContext httpContext,
        CancellationToken ct
    )
    {
        var context = await authorizationService.RequireAuthenticatedAsync(httpContext, ct);

        return await dbContext
            .Resources.Where(resource =>
                resource.CreatedByUserId == context.UserId && resource.DeletedAt == null
            )
            .OrderBy(resource => resource.Name)
            .Select(resource => ToDto(resource))
            .ToArrayAsync(ct);
    }

    public async Task<ResourceDto> GetResourceAsync(
        HttpContext httpContext,
        Guid resourceId,
        CancellationToken ct
    )
    {
        var context = await authorizationService.RequireAuthenticatedAsync(httpContext, ct);

        var resource =
            await dbContext.Resources.SingleOrDefaultAsync(
                item =>
                    item.Id == resourceId
                    && item.CreatedByUserId == context.UserId
                    && item.DeletedAt == null,
                ct
            )
            ?? throw new ServiceException("Resource was not found.", StatusCodes.Status404NotFound);

        return ToDto(resource);
    }

    public async Task<ResourceDto> CreateResourceAsync(
        HttpContext httpContext,
        CreateResourceCommand command,
        CancellationToken ct
    )
    {
        var context = await authorizationService.RequireAuthenticatedAsync(httpContext, ct);
        var name = RequireValue(command.Name, "Resource name is required.", 200);
        var description = NormalizeOptional(command.Description, 1000);
        var now = clock.GetCurrentInstant();
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedByUserId = context.UserId,
            CreatedAt = now,
        };

        dbContext.Resources.Add(resource);
        await dbContext.SaveChangesAsync(ct);

        return ToDto(resource);
    }

    private static ResourceDto ToDto(Resource resource)
    {
        return new ResourceDto(
            resource.Id,
            resource.Name,
            resource.Description,
            resource.CreatedAt.ToString()
        );
    }

    private static string RequireValue(
        string? value,
        string errorMessage,
        int maxLength = int.MaxValue
    )
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            throw new ServiceException(errorMessage, StatusCodes.Status400BadRequest);
        }

        if (normalized.Length > maxLength)
        {
            throw new ServiceException(
                $"Value must be {maxLength} characters or fewer.",
                StatusCodes.Status400BadRequest
            );
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength = int.MaxValue)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ServiceException(
                $"Value must be {maxLength} characters or fewer.",
                StatusCodes.Status400BadRequest
            );
        }

        return normalized;
    }

    public sealed record CreateResourceCommand(string? Name, string? Description);
}
