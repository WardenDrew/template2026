const PROFILE = "TEMPLATE-OPAQUE-P256-SHA256-V1";
const VERSION = 1;
const FIELD_BYTE_LENGTH = 32;
const POINT_BYTE_LENGTH = 65;
const POINT_PREFIX = 4;

const PRIME = hexToBigInt(
  "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF"
);
const ORDER = hexToBigInt(
  "FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551"
);
const CURVE_B = hexToBigInt(
  "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B"
);
const GENERATOR = {
  infinity: false,
  x: hexToBigInt(
    "6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296"
  ),
  y: hexToBigInt(
    "4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5"
  )
} satisfies P256Point;
const INFINITY = { infinity: true, x: 0n, y: 0n } satisfies P256Point;

const textEncoder = new TextEncoder();
const HASH_TO_SCALAR_LABEL = utf8(`${PROFILE}:hash-to-scalar`);
const OPRF_FINALIZE_LABEL = utf8(`${PROFILE}:oprf-finalize`);
const ENVELOPE_LABEL = utf8(`${PROFILE}:envelope`);
const EXPORT_LABEL = utf8(`${PROFILE}:export`);
const SESSION_LABEL = utf8(`${PROFILE}:session`);
const CLIENT_MAC_LABEL = utf8(`${PROFILE}:client-finish`);

type P256Point = {
  infinity: boolean;
  x: bigint;
  y: bigint;
};

export type OpaqueClientStart = {
  request: {
    blindedElementBase64: string;
  };
  state: OpaqueClientStartState;
};

export type OpaqueClientStartState = {
  blindBase64: string;
  blindedElementBase64: string;
  passwordScalarBase64: string;
};

export type OpaqueRegistrationStartResponse = {
  serverKeyId: string;
  serverPublicKeyBase64: string;
  evaluatedElementBase64: string;
};

export type OpaqueRegistrationFinish = {
  exportKeyBase64: string;
  passwordWrapKeyBase64: string;
  registrationRecordJson: string;
};

export type OpaqueLoginStartResponse = {
  serverKeyId: string;
  evaluatedElementBase64: string;
  serverPublicKeyBase64: string;
  clientPublicKeyBase64: string;
  envelopeNonceBase64: string;
  envelopeCiphertextBase64: string;
  serverNonceBase64: string;
  serverEphemeralPublicKeyBase64: string;
};

export type OpaqueLoginFinish = {
  exportKeyBase64: string;
  request: {
    clientNonceBase64: string;
    clientEphemeralPublicKeyBase64: string;
    clientMacBase64: string;
  };
};

type OpaqueEnvelopePlaintext = {
  version: number;
  profile: string;
  serverKeyId: string;
  clientPrivateKeyBase64: string;
  serverPublicKeyBase64: string;
};

export async function beginOpaqueRegistration(
  password: string
): Promise<OpaqueClientStart> {
  return beginOpaquePassword(password);
}

export async function finishOpaqueRegistration(
  state: OpaqueClientStartState,
  response: OpaqueRegistrationStartResponse
): Promise<OpaqueRegistrationFinish> {
  const oprfOutput = await finalizeOprf(
    state,
    response.evaluatedElementBase64
  );
  const exportKey = await hkdfSha256(oprfOutput, new Uint8Array(), EXPORT_LABEL);
  const envelopeKey = await hkdfSha256(
    oprfOutput,
    new Uint8Array(),
    ENVELOPE_LABEL
  );
  const clientPrivateScalar = randomScalar();
  const clientPublicKeyBase64 = pointToBase64(
    scalarMultiply(clientPrivateScalar, GENERATOR)
  );
  const envelopeNonce = randomBytes(12);
  const envelopePlaintext: OpaqueEnvelopePlaintext = {
    clientPrivateKeyBase64: scalarToBase64(clientPrivateScalar),
    profile: PROFILE,
    serverKeyId: response.serverKeyId,
    serverPublicKeyBase64: response.serverPublicKeyBase64,
    version: VERSION
  };
  const envelopeCiphertext = await aesGcmEncrypt(
    envelopeKey,
    envelopeNonce,
    utf8(JSON.stringify(envelopePlaintext))
  );
  const registrationRecordJson = JSON.stringify({
    clientPublicKeyBase64,
    envelope: {
      ciphertextBase64: toBase64(envelopeCiphertext),
      nonceBase64: toBase64(envelopeNonce)
    },
    profile: PROFILE,
    serverKeyId: response.serverKeyId,
    serverPublicKeyBase64: response.serverPublicKeyBase64,
    version: VERSION
  });

  return {
    exportKeyBase64: toBase64(exportKey),
    passwordWrapKeyBase64: toBase64(exportKey),
    registrationRecordJson
  };
}

export async function beginOpaqueLogin(
  password: string
): Promise<OpaqueClientStart> {
  return beginOpaquePassword(password);
}

export async function finishOpaqueLogin(
  state: OpaqueClientStartState,
  response: OpaqueLoginStartResponse
): Promise<OpaqueLoginFinish> {
  const oprfOutput = await finalizeOprf(
    state,
    response.evaluatedElementBase64
  );
  const exportKey = await hkdfSha256(oprfOutput, new Uint8Array(), EXPORT_LABEL);
  const envelopeKey = await hkdfSha256(
    oprfOutput,
    new Uint8Array(),
    ENVELOPE_LABEL
  );
  const envelopePlaintextBytes = await aesGcmDecrypt(
    envelopeKey,
    fromBase64(response.envelopeNonceBase64),
    fromBase64(response.envelopeCiphertextBase64)
  );
  const envelopePlaintext = parseEnvelopePlaintext(envelopePlaintextBytes);

  if (
    envelopePlaintext.version !== VERSION ||
    envelopePlaintext.profile !== PROFILE ||
    envelopePlaintext.serverKeyId !== response.serverKeyId ||
    envelopePlaintext.serverPublicKeyBase64 !== response.serverPublicKeyBase64
  ) {
    throw new Error("OPAQUE envelope does not match the server response.");
  }

  const clientPrivateScalar = scalarFromBase64(
    envelopePlaintext.clientPrivateKeyBase64,
    "client private key"
  );
  const serverPublicKey = parsePoint(
    fromBase64(response.serverPublicKeyBase64),
    "server public key"
  );
  const serverEphemeralPublicKey = parsePoint(
    fromBase64(response.serverEphemeralPublicKeyBase64),
    "server ephemeral public key"
  );
  const clientEphemeralPrivateScalar = randomScalar();
  const clientEphemeralPublicKeyBase64 = pointToBase64(
    scalarMultiply(clientEphemeralPrivateScalar, GENERATOR)
  );
  const clientNonceBase64 = toBase64(randomBytes(32));
  const dh1 = xCoordinate(
    scalarMultiply(clientPrivateScalar, serverPublicKey)
  );
  const dh2 = xCoordinate(
    scalarMultiply(clientEphemeralPrivateScalar, serverPublicKey)
  );
  const dh3 = xCoordinate(
    scalarMultiply(clientPrivateScalar, serverEphemeralPublicKey)
  );
  const sharedSecret = concatBytes(dh1, dh2, dh3);
  const transcriptHash = await computeTranscriptHash(
    state,
    response,
    clientNonceBase64,
    clientEphemeralPublicKeyBase64
  );
  const sessionKey = await hkdfSha256(
    sharedSecret,
    transcriptHash,
    SESSION_LABEL
  );
  const clientMac = await hmacSha256(
    sessionKey,
    concatBytes(CLIENT_MAC_LABEL, transcriptHash)
  );

  return {
    exportKeyBase64: toBase64(exportKey),
    request: {
      clientEphemeralPublicKeyBase64,
      clientMacBase64: toBase64(clientMac),
      clientNonceBase64
    }
  };
}

async function beginOpaquePassword(
  password: string
): Promise<OpaqueClientStart> {
  const passwordScalar = await hashPasswordToScalar(password);
  const passwordPoint = scalarMultiply(passwordScalar, GENERATOR);
  const blind = randomScalar();
  const blindedPoint = scalarMultiply(blind, passwordPoint);
  const blindedElementBase64 = pointToBase64(blindedPoint);

  return {
    request: {
      blindedElementBase64
    },
    state: {
      blindBase64: scalarToBase64(blind),
      blindedElementBase64,
      passwordScalarBase64: scalarToBase64(passwordScalar)
    }
  };
}

async function finalizeOprf(
  state: OpaqueClientStartState,
  evaluatedElementBase64: string
) {
  const blind = scalarFromBase64(state.blindBase64, "blind");
  const passwordScalarBytes = fromBase64(state.passwordScalarBase64);
  const evaluatedElement = parsePoint(
    fromBase64(evaluatedElementBase64),
    "evaluated element"
  );
  const unblindedElement = scalarMultiply(invertScalar(blind), evaluatedElement);

  return sha256(
    concatBytes(
      OPRF_FINALIZE_LABEL,
      passwordScalarBytes,
      serializePoint(unblindedElement)
    )
  );
}

async function computeTranscriptHash(
  state: OpaqueClientStartState,
  response: OpaqueLoginStartResponse,
  clientNonceBase64: string,
  clientEphemeralPublicKeyBase64: string
) {
  return sha256(
    concatLengthPrefixed(
      utf8(PROFILE),
      fromBase64(state.blindedElementBase64),
      fromBase64(response.evaluatedElementBase64),
      fromBase64(response.clientPublicKeyBase64),
      fromBase64(response.serverPublicKeyBase64),
      fromBase64(clientEphemeralPublicKeyBase64),
      fromBase64(response.serverEphemeralPublicKeyBase64),
      fromBase64(clientNonceBase64),
      fromBase64(response.serverNonceBase64),
      fromBase64(response.envelopeNonceBase64),
      fromBase64(response.envelopeCiphertextBase64)
    )
  );
}

async function hashPasswordToScalar(password: string) {
  const digest = await crypto.subtle.digest(
    "SHA-512",
    toArrayBuffer(concatBytes(HASH_TO_SCALAR_LABEL, utf8(password)))
  );

  return mod(bytesToBigInt(new Uint8Array(digest)), ORDER - 1n) + 1n;
}

async function aesGcmEncrypt(
  keyBytes: Uint8Array,
  nonce: Uint8Array,
  plaintext: Uint8Array
) {
  const key = await crypto.subtle.importKey(
    "raw",
    toArrayBuffer(keyBytes),
    { name: "AES-GCM" },
    false,
    ["encrypt"]
  );
  const ciphertext = await crypto.subtle.encrypt(
    {
      iv: toArrayBuffer(nonce),
      name: "AES-GCM"
    },
    key,
    toArrayBuffer(plaintext)
  );

  return new Uint8Array(ciphertext);
}

async function aesGcmDecrypt(
  keyBytes: Uint8Array,
  nonce: Uint8Array,
  ciphertext: Uint8Array
) {
  const key = await crypto.subtle.importKey(
    "raw",
    toArrayBuffer(keyBytes),
    { name: "AES-GCM" },
    false,
    ["decrypt"]
  );
  const plaintext = await crypto.subtle.decrypt(
    {
      iv: toArrayBuffer(nonce),
      name: "AES-GCM"
    },
    key,
    toArrayBuffer(ciphertext)
  );

  return new Uint8Array(plaintext);
}

async function hkdfSha256(
  inputKeyMaterial: Uint8Array,
  salt: Uint8Array,
  info: Uint8Array,
  length = 32
) {
  const actualSalt = salt.length === 0 ? new Uint8Array(32) : salt;
  const pseudorandomKey = await hmacSha256(actualSalt, inputKeyMaterial);
  const blocks: Uint8Array[] = [];
  let previous = new Uint8Array();
  let outputLength = 0;
  let counter = 1;

  while (outputLength < length) {
    previous = await hmacSha256(
      pseudorandomKey,
      concatBytes(previous, info, new Uint8Array([counter]))
    );
    blocks.push(previous);
    outputLength += previous.length;
    counter++;
  }

  return concatBytes(...blocks).slice(0, length);
}

async function hmacSha256(keyBytes: Uint8Array, value: Uint8Array) {
  const key = await crypto.subtle.importKey(
    "raw",
    toArrayBuffer(keyBytes),
    {
      hash: "SHA-256",
      name: "HMAC"
    },
    false,
    ["sign"]
  );
  const signature = await crypto.subtle.sign(
    "HMAC",
    key,
    toArrayBuffer(value)
  );

  return new Uint8Array(signature);
}

async function sha256(value: Uint8Array) {
  return new Uint8Array(
    await crypto.subtle.digest("SHA-256", toArrayBuffer(value))
  );
}

function parseEnvelopePlaintext(value: Uint8Array): OpaqueEnvelopePlaintext {
  const decoded = JSON.parse(new TextDecoder().decode(value)) as Partial<
    OpaqueEnvelopePlaintext
  >;

  if (
    typeof decoded.version !== "number" ||
    typeof decoded.profile !== "string" ||
    typeof decoded.serverKeyId !== "string" ||
    typeof decoded.clientPrivateKeyBase64 !== "string" ||
    typeof decoded.serverPublicKeyBase64 !== "string"
  ) {
    throw new Error("OPAQUE envelope plaintext is invalid.");
  }

  return {
    clientPrivateKeyBase64: decoded.clientPrivateKeyBase64,
    profile: decoded.profile,
    serverKeyId: decoded.serverKeyId,
    serverPublicKeyBase64: decoded.serverPublicKeyBase64,
    version: decoded.version
  };
}

function scalarMultiply(scalar: bigint, point: P256Point): P256Point {
  let k = mod(scalar, ORDER);

  if (k === 0n || point.infinity) {
    return INFINITY;
  }

  let result: P256Point = INFINITY;
  let addend: P256Point = point;

  while (k > 0n) {
    if ((k & 1n) === 1n) {
      result = addPoints(result, addend);
    }

    addend = addPoints(addend, addend);
    k >>= 1n;
  }

  return result;
}

function addPoints(left: P256Point, right: P256Point): P256Point {
  if (left.infinity) {
    return right;
  }

  if (right.infinity) {
    return left;
  }

  if (left.x === right.x) {
    if (mod(left.y + right.y, PRIME) === 0n) {
      return INFINITY;
    }

    return doublePoint(left);
  }

  const lambda = mod(
    (right.y - left.y) * invertField(right.x - left.x),
    PRIME
  );
  const x = mod(lambda * lambda - left.x - right.x, PRIME);
  const y = mod(lambda * (left.x - x) - left.y, PRIME);

  return { infinity: false, x, y };
}

function doublePoint(point: P256Point): P256Point {
  if (point.infinity || point.y === 0n) {
    return INFINITY;
  }

  const lambda = mod(
    (3n * point.x * point.x - 3n) * invertField(2n * point.y),
    PRIME
  );
  const x = mod(lambda * lambda - 2n * point.x, PRIME);
  const y = mod(lambda * (point.x - x) - point.y, PRIME);

  return { infinity: false, x, y };
}

function parsePoint(bytes: Uint8Array, label: string): P256Point {
  if (bytes.length !== POINT_BYTE_LENGTH || bytes[0] !== POINT_PREFIX) {
    throw new Error(`${label} must be an uncompressed P-256 point.`);
  }

  const x = bytesToBigInt(bytes.slice(1, 33));
  const y = bytesToBigInt(bytes.slice(33, 65));
  const point = { infinity: false, x, y } satisfies P256Point;

  if (!isValidPoint(point)) {
    throw new Error(`${label} is not on the P-256 curve.`);
  }

  return point;
}

function serializePoint(point: P256Point) {
  if (point.infinity) {
    throw new Error("Cannot serialize point at infinity.");
  }

  return concatBytes(
    new Uint8Array([POINT_PREFIX]),
    bigIntToFixedBytes(point.x),
    bigIntToFixedBytes(point.y)
  );
}

function pointToBase64(point: P256Point) {
  return toBase64(serializePoint(point));
}

function isValidPoint(point: P256Point) {
  if (
    point.infinity ||
    point.x < 0n ||
    point.x >= PRIME ||
    point.y < 0n ||
    point.y >= PRIME
  ) {
    return false;
  }

  const left = mod(point.y * point.y, PRIME);
  const right = mod(point.x * point.x * point.x - 3n * point.x + CURVE_B, PRIME);

  return left === right;
}

function xCoordinate(point: P256Point) {
  if (point.infinity) {
    throw new Error("Shared point was infinity.");
  }

  return bigIntToFixedBytes(point.x);
}

function randomScalar() {
  while (true) {
    const scalar = bytesToBigInt(randomBytes(FIELD_BYTE_LENGTH));

    if (scalar > 0n && scalar < ORDER) {
      return scalar;
    }
  }
}

function invertScalar(value: bigint) {
  return modPow(mod(value, ORDER), ORDER - 2n, ORDER);
}

function invertField(value: bigint) {
  return modPow(mod(value, PRIME), PRIME - 2n, PRIME);
}

function scalarFromBase64(value: string, label: string) {
  const bytes = fromBase64(value);

  if (bytes.length !== FIELD_BYTE_LENGTH) {
    throw new Error(`${label} must be a 32-byte scalar.`);
  }

  const scalar = bytesToBigInt(bytes);

  if (scalar <= 0n || scalar >= ORDER) {
    throw new Error(`${label} is outside the P-256 scalar range.`);
  }

  return scalar;
}

function scalarToBase64(value: bigint) {
  const scalar = mod(value, ORDER);

  if (scalar === 0n) {
    throw new Error("Scalar must be non-zero.");
  }

  return toBase64(bigIntToFixedBytes(scalar));
}

function modPow(value: bigint, exponent: bigint, modulus: bigint) {
  let result = 1n;
  let base = mod(value, modulus);
  let power = exponent;

  while (power > 0n) {
    if ((power & 1n) === 1n) {
      result = mod(result * base, modulus);
    }

    base = mod(base * base, modulus);
    power >>= 1n;
  }

  return result;
}

function mod(value: bigint, modulus: bigint) {
  const result = value % modulus;

  return result < 0n ? result + modulus : result;
}

function concatLengthPrefixed(...values: Uint8Array[]) {
  const length = values.reduce((sum, value) => sum + 4 + value.length, 0);
  const result = new Uint8Array(length);
  const view = new DataView(result.buffer);
  let offset = 0;

  for (const value of values) {
    view.setInt32(offset, value.length, false);
    offset += 4;
    result.set(value, offset);
    offset += value.length;
  }

  return result;
}

function concatBytes(...values: Uint8Array[]) {
  const length = values.reduce((sum, value) => sum + value.length, 0);
  const result = new Uint8Array(length);
  let offset = 0;

  for (const value of values) {
    result.set(value, offset);
    offset += value.length;
  }

  return result;
}

function randomBytes(length: number) {
  const bytes = new Uint8Array(length);

  crypto.getRandomValues(bytes);

  return bytes;
}

function toArrayBuffer(bytes: Uint8Array): ArrayBuffer {
  return new Uint8Array(bytes).buffer as ArrayBuffer;
}

function utf8(value: string) {
  return textEncoder.encode(value);
}

function fromBase64(value: string) {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);

  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  return bytes;
}

function toBase64(data: Uint8Array) {
  let binary = "";

  for (const byte of data) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary);
}

function bytesToBigInt(bytes: Uint8Array) {
  let value = 0n;

  for (const byte of bytes) {
    value = (value << 8n) | BigInt(byte);
  }

  return value;
}

function bigIntToFixedBytes(value: bigint) {
  const result = new Uint8Array(FIELD_BYTE_LENGTH);
  let remaining = value;

  for (let index = FIELD_BYTE_LENGTH - 1; index >= 0; index--) {
    result[index] = Number(remaining & 0xffn);
    remaining >>= 8n;
  }

  if (remaining !== 0n) {
    throw new Error("Value is too large.");
  }

  return result;
}

function hexToBigInt(value: string) {
  return BigInt(`0x${value}`);
}
