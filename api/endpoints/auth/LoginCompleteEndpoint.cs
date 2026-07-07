using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class LoginCompleteEndpoint(AuthenticationService authenticationService)
    : Endpoint<LoginCompleteEndpoint.RequestDto, AuthenticationService.LoginCompleteResult>
{
    public override void Configure()
    {
        Post("/auth/login/complete");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await authenticationService.CompleteLoginAsync(
                    request.LoginToken,
                    cancellationToken
                ),
                cancellationToken
            );
        }
        catch (AuthServiceException exception)
        {
            ValidationContext.Instance.ThrowError(exception.Message, exception.StatusCode);
        }
    }

    public sealed class RequestDto
    {
        public string? LoginToken { get; init; }
    }
}
