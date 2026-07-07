using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class LoginTotpVerifyEndpoint(AuthenticationService authenticationService)
    : Endpoint<LoginTotpVerifyEndpoint.RequestDto, AuthenticationService.LoginTotpVerifyResult>
{
    public override void Configure()
    {
        Post("/auth/login/totp/verify");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await authenticationService.VerifyLoginTotpAsync(
                    request.LoginToken,
                    request.Code,
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

        public string? Code { get; init; }
    }
}
