using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class LogoutEndpoint(AuthenticationService authenticationService)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/logout");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await authenticationService.LogoutAsync(HttpContext, cancellationToken);
            await Send.NoContentAsync(cancellationToken);
        }
        catch (AuthServiceException exception)
        {
            ValidationContext.Instance.ThrowError(exception.Message, exception.StatusCode);
        }
    }
}
