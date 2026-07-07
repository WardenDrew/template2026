namespace TemplateApi.Services;

public sealed class AuthorizationService(AuthenticationService authenticationService)
{
    public async Task<AuthenticatedUserContext> RequireAuthenticatedAsync(
        HttpContext httpContext,
        CancellationToken ct
    )
    {
        try
        {
            var context = await authenticationService.AuthenticateAccessTokenAsync(httpContext, ct);

            return new AuthenticatedUserContext(context.User.Id);
        }
        catch (AuthServiceException exception)
        {
            throw new ServiceException(exception.Message, exception.StatusCode);
        }
    }

    public sealed record AuthenticatedUserContext(Guid UserId);
}
