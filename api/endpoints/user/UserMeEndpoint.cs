using FastEndpoints;
using TemplateApi.Models;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.User;

public sealed class UserMeEndpoint(AuthenticationService authenticationService)
    : EndpointWithoutRequest<SimpleUserDto>
{
    public override void Configure()
    {
        Get("/user/me");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await authenticationService.GetCurrentUserAsync(HttpContext, cancellationToken),
                cancellationToken
            );
        }
        catch (AuthServiceException exception)
        {
            ValidationContext.Instance.ThrowError(exception.Message, exception.StatusCode);
        }
    }
}
