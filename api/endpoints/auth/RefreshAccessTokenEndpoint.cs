using System.Text.Json;
using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Endpoints.Auth;

public sealed class RefreshAccessTokenEndpoint(AuthenticationService authenticationService)
    : EndpointWithoutRequest<AuthenticationService.RefreshAccessTokenResult>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = await ReadRequestAsync(cancellationToken);

            await Send.OkAsync(
                await authenticationService.RefreshAccessTokenAsync(
                    HttpContext,
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
        catch (JsonException)
        {
            ValidationContext.Instance.ThrowError(
                "Refresh request body is invalid JSON.",
                StatusCodes.Status400BadRequest
            );
        }
    }

    public sealed class RequestDto
    {
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

    private async Task<RequestDto> ReadRequestAsync(CancellationToken cancellationToken)
    {
        if (HttpContext.Request.ContentLength is null or 0)
        {
            return new RequestDto();
        }

        return await JsonSerializer.DeserializeAsync<RequestDto>(
                HttpContext.Request.Body,
                SerializerOptions,
                cancellationToken: cancellationToken
            ) ?? new RequestDto();
    }
}
