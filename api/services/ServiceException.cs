namespace TemplateApi.Services;

public sealed class ServiceException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
