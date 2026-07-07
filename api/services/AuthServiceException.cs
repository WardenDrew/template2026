namespace TemplateApi.Services;

public sealed class AuthServiceException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
