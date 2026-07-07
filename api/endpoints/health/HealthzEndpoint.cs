using FastEndpoints;
using NodaTime;

namespace TemplateApi.Endpoints.Health;

public sealed class HealthzEndpoint(IClock clock)
    : EndpointWithoutRequest<HealthzEndpoint.ResponseDto>
{
    public override void Configure()
    {
        Get("/healthz");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var response = new ResponseDto(
            Status: "ok",
            CheckedAt: clock.GetCurrentInstant().ToString()
        );

        await Send.OkAsync(response, cancellationToken);
    }

    public sealed record ResponseDto(string Status, string CheckedAt);
}
