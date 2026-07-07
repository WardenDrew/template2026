using FastEndpoints;
using TemplateApi.Models;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Resources;

public sealed class ListResourcesEndpoint(ResourceService resourceService)
    : EndpointWithoutRequest<IReadOnlyList<ResourceDto>>
{
    public override void Configure()
    {
        Get("/resources");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await resourceService.ListCurrentUserResourcesAsync(HttpContext, cancellationToken),
                cancellationToken
            );
        }
        catch (ServiceException exception)
        {
            ValidationContext.Instance.ThrowError(exception.Message, exception.StatusCode);
        }
    }
}
