using FastEndpoints;
using TemplateApi.Models;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Resources;

public sealed class CreateResourceEndpoint(ResourceService resourceService)
    : Endpoint<CreateResourceEndpoint.RequestDto, ResourceDto>
{
    public override void Configure()
    {
        Post("/resources");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await resourceService.CreateResourceAsync(
                    HttpContext,
                    new ResourceService.CreateResourceCommand(request.Name, request.Description),
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
        public string? Name { get; init; }

        public string? Description { get; init; }
    }
}
