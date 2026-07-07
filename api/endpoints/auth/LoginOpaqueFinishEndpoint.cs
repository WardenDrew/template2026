using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class LoginOpaqueFinishEndpoint(AuthenticationService authenticationService)
    : Endpoint<LoginOpaqueFinishEndpoint.RequestDto, AuthenticationService.LoginOpaqueFinishResult>
{
    public override void Configure()
    {
        Post("/auth/login/opaque/finish");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await authenticationService.FinishLoginOpaqueAsync(
                    request.LoginToken,
                    request.ClientNonceBase64,
                    request.ClientEphemeralPublicKeyBase64,
                    request.ClientMacBase64,
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

        public string? ClientNonceBase64 { get; init; }

        public string? ClientEphemeralPublicKeyBase64 { get; init; }

        public string? ClientMacBase64 { get; init; }
    }
}
