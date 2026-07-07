using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class RegisterPasswordSetupEndpoint(RegistrationService registrationService)
    : Endpoint<
        RegisterPasswordSetupEndpoint.RequestDto,
        RegistrationService.RegistrationPasswordSetupResult
    >
{
    public override void Configure()
    {
        Post("/auth/register/password");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await registrationService.SetupRegistrationPasswordAsync(
                    request.RegistrationToken,
                    request.OpaqueRegistrationRecordJson,
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

        public string? OpaqueRegistrationRecordJson { get; init; }
    }
}
