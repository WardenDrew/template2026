using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class RegisterTotpSetupEndpoint(RegistrationService registrationService)
    : Endpoint<
        RegisterTotpSetupEndpoint.RequestDto,
        RegistrationService.RegistrationTotpSetupResult
    >
{
    public override void Configure()
    {
        Post("/auth/register/totp");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await registrationService.SetupRegistrationTotpAsync(
                    request.RegistrationToken,
                    request.TotpSecret,
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
        public string? RegistrationToken { get; init; }

        public string? TotpSecret { get; init; }

        public string? Code { get; init; }
    }
}
