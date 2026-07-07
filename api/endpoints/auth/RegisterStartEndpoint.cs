using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class RegisterStartEndpoint(RegistrationService registrationService)
    : Endpoint<RegisterStartEndpoint.RequestDto, RegistrationService.RegistrationStartResult>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await registrationService.StartRegistrationAsync(
                    request.Email,
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
        public string? Email { get; init; }

        public string? DisplayName { get; init; }
    }
}
