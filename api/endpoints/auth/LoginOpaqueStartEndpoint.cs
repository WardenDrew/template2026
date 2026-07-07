using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class LoginOpaqueStartEndpoint(AuthenticationService authenticationService)
    : Endpoint<LoginOpaqueStartEndpoint.RequestDto, AuthenticationService.LoginOpaqueStartResult>
{
    public override void Configure()
    {
        Post("/auth/login/opaque/start");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await authenticationService.StartLoginOpaqueAsync(
                    request.LoginToken,
                    request.BlindedElementBase64,
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

        public string? BlindedElementBase64 { get; init; }
    }
}
