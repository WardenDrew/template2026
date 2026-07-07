using FastEndpoints;
using TemplateApi.Models;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Resources;

public sealed class GetResourceEndpoint(ResourceService resourceService)
    : Endpoint<GetResourceEndpoint.RequestDto, ResourceDto>
{
    public override void Configure()
    {
        Get("/resources/{ResourceId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await resourceService.GetResourceAsync(
                    HttpContext,
                    request.ResourceId,
                    cancellationToken
                ),
                cancellationToken
            );
        }
        catch (ServiceException exception)
        {
            ValidationContext.Instance.ThrowError(exception.Message, exception.StatusCode);
        }
    }

    public sealed class RequestDto
    {
        [RouteParam]
        public Guid ResourceId { get; init; }
    }
}
