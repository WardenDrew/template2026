namespace Template.Opaque;

public sealed record OpaqueRegistrationStartResponse(
    string ServerKeyId,
    string ServerPublicKeyBase64,
    string EvaluatedElementBase64
);

public sealed record OpaqueLoginStartResponse(
    string ServerKeyId,
    string EvaluatedElementBase64,
    string ServerPublicKeyBase64,
    string ClientPublicKeyBase64,
    string EnvelopeNonceBase64,
    string EnvelopeCiphertextBase64,
    string ServerNonceBase64,
    string ServerEphemeralPublicKeyBase64,
    OpaqueLoginState State
);

public sealed record OpaqueLoginState(
    string ServerKeyId,
    string BlindedElementBase64,
    string EvaluatedElementBase64,
    string ServerNonceBase64,
    string ServerEphemeralPrivateKeyBase64,
    string ServerEphemeralPublicKeyBase64,
    string RegistrationRecordHashBase64
);

public sealed class OpaqueRegistrationRecord
{
    public int Version { get; init; }

    public string Profile { get; init; } = string.Empty;

    public string ServerKeyId { get; init; } = string.Empty;

    public string ClientPublicKeyBase64 { get; init; } = string.Empty;

    public string ServerPublicKeyBase64 { get; init; } = string.Empty;

    public OpaqueEnvelope Envelope { get; init; } = new();
}

public sealed class OpaqueEnvelope
{
    public string NonceBase64 { get; init; } = string.Empty;

    public string CiphertextBase64 { get; init; } = string.Empty;
}
