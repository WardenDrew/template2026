using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class RegisterPasswordStartEndpoint(RegistrationService registrationService)
    : Endpoint<
        RegisterPasswordStartEndpoint.RequestDto,
        RegistrationService.RegistrationPasswordStartResult
    >
{
    public override void Configure()
    {
        Post("/auth/register/password/start");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await registrationService.StartRegistrationPasswordAsync(
                    request.RegistrationToken,
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
        public string? RegistrationToken { get; init; }

        public string? BlindedElementBase64 { get; init; }
    }
}
