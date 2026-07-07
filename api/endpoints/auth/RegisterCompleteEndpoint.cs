using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class RegisterCompleteEndpoint(RegistrationService registrationService)
    : Endpoint<RegisterCompleteEndpoint.RequestDto, RegistrationService.RegistrationCompleteResult>
{
    public override void Configure()
    {
        Post("/auth/register/complete");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await registrationService.CompleteRegistrationAsync(
                    request.RegistrationToken,
                    request.DisplayName,
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

        public string? DisplayName { get; init; }
    }
}
