using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class LoginStartEndpoint(AuthenticationService authenticationService)
    : Endpoint<LoginStartEndpoint.RequestDto, AuthenticationService.LoginStartResult>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await Send.OkAsync(
                await authenticationService.StartLoginAsync(
                    request.Email,
                    request.GetRequestedScopes(),
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

        public string? Scope { get; init; }

        public IReadOnlyList<string>? Scopes { get; init; }

        public IReadOnlyList<string>? GetRequestedScopes()
        {
            var requestedScopes = new List<string>();

            if (!string.IsNullOrWhiteSpace(Scope))
            {
                requestedScopes.Add(Scope);
            }

            if (Scopes is not null)
            {
                requestedScopes.AddRange(Scopes);
            }

            return requestedScopes.Count == 0 ? null : requestedScopes;
        }
    }
}
