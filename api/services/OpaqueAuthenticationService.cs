using Microsoft.Extensions.Options;
using Template.Opaque;
using TemplateApi.Options;

namespace TemplateApi.Services;

public sealed class OpaqueAuthenticationService
{
    private readonly OpaqueServer server;

    public OpaqueAuthenticationService(IOptions<AuthenticationOptions> authenticationOptions)
    {
        var options = authenticationOptions.Value;
        var keys = options
            .OpaqueServerKeys.Select(key =>
                OpaqueServerKey.FromPrivateKeyBase64(key.KeyId, key.PrivateKeyBase64)
            )
            .ToArray();

        server = new OpaqueServer(keys, options.OpaqueActiveKeyId);
    }

    public OpaqueRegistrationStartResponse CreateRegistrationStart(string? blindedElementBase64)
    {
        return RunOpaqueOperation(() =>
            server.CreateRegistrationStart(
                RequireValue(blindedElementBase64, "OPAQUE blinded element is required.")
            )
        );
    }

    public OpaqueLoginStartResponse CreateLoginStart(
        string? blindedElementBase64,
        string registrationRecordJson
    )
    {
        return RunOpaqueOperation(() =>
            server.CreateLoginStart(
                RequireValue(blindedElementBase64, "OPAQUE blinded element is required."),
                registrationRecordJson
            )
        );
    }

    public void ValidateRegistrationRecord(string registrationRecordJson)
    {
        RunOpaqueOperation(() => server.ValidateRegistrationRecord(registrationRecordJson));
    }

    public string SerializeLoginState(OpaqueLoginState state)
    {
        return OpaqueServer.SerializeLoginState(state);
    }

    public OpaqueLoginState DeserializeLoginState(string stateJson)
    {
        return RunOpaqueOperation(() => OpaqueServer.DeserializeLoginState(stateJson));
    }

    public bool VerifyLoginFinish(
        string registrationRecordJson,
        OpaqueLoginState state,
        string? clientNonceBase64,
        string? clientEphemeralPublicKeyBase64,
        string? clientMacBase64
    )
    {
        return RunOpaqueOperation(() =>
            server.VerifyLoginFinish(
                registrationRecordJson,
                state,
                RequireValue(clientNonceBase64, "OPAQUE client nonce is required."),
                RequireValue(
                    clientEphemeralPublicKeyBase64,
                    "OPAQUE client ephemeral public key is required."
                ),
                RequireValue(clientMacBase64, "OPAQUE client MAC is required.")
            )
        );
    }

    private static T RunOpaqueOperation<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (OpaqueProtocolException exception)
        {
            throw new AuthServiceException(exception.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static void RunOpaqueOperation(Action operation)
    {
        try
        {
            operation();
        }
        catch (OpaqueProtocolException exception)
        {
            throw new AuthServiceException(exception.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static string RequireValue(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AuthServiceException(message, StatusCodes.Status400BadRequest);
        }

        return value.Trim();
    }
}
