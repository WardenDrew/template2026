<template>
  <q-form class="column q-gutter-y-md" @submit.prevent="handleSubmit">
    <q-banner v-if="errorMessage" rounded class="bg-negative text-white">
      {{ errorMessage }}
    </q-banner>

    <q-banner v-if="statusMessage" rounded class="bg-positive text-white">
      {{ statusMessage }}
    </q-banner>

    <template v-if="step === 'account'">
      <q-input
        ref="emailInput"
        v-model="email"
        outlined
        type="email"
        label="Email"
        autocomplete="email"
        :disable="busy"
        :error="showEmailError"
        :error-message="emailErrorMessage"
        @blur="emailTouched = true"
      />

      <q-input
        v-model="displayName"
        outlined
        label="Display name"
        autocomplete="name"
        :disable="busy"
      />

      <q-btn
        unelevated
        color="primary"
        type="submit"
        icon-right="mdi-arrow-right"
        label="Continue"
        :disable="busy || !isEmailValid"
        :loading="busy"
        no-caps
      />
    </template>

    <template v-else-if="step === 'passwordSetup'">
      <div class="text-body2 text-grey-5">
        Choose a password. It is used from this device, and the password never
        leaves this device.
      </div>

      <q-input
        ref="passwordInput"
        v-model="password"
        outlined
        type="password"
        label="Password"
        autocomplete="new-password"
        :disable="busy"
      />

      <q-input
        v-model="confirmPassword"
        outlined
        type="password"
        label="Confirm password"
        autocomplete="new-password"
        :disable="busy"
        :error="showPasswordMismatch"
        error-message="Passwords must match."
        @blur="confirmPasswordTouched = true"
      />

      <q-btn
        unelevated
        color="primary"
        type="submit"
        icon-right="mdi-key"
        label="Set password"
        :disable="busy || !passwordReady"
        :loading="busy"
        no-caps
      />
    </template>

    <template v-else-if="step === 'totp'">
      <q-banner rounded class="bg-grey-10 text-grey-2">
        Scan the QR code with an authenticator app, then enter the current 6
        digit code.
      </q-banner>

      <div class="totp-setup">
        <div class="totp-qr" aria-label="Authenticator QR code">
          <svg
            v-if="totpQrCode"
            class="totp-qr__image"
            :viewBox="totpQrCode.viewBox"
            role="img"
            aria-labelledby="totp-qr-title"
            shape-rendering="crispEdges"
          >
            <title id="totp-qr-title">Authenticator setup QR code</title>
            <rect width="100%" height="100%" fill="#fff" />
            <path :d="totpQrCode.path" fill="#111827" />
          </svg>

          <div v-else class="text-caption text-grey-8 text-center">
            Use the setup key
          </div>
        </div>

        <q-input
          v-model="totpSecret"
          outlined
          readonly
          label="Manual setup key"
        >
          <template #append>
            <q-btn
              flat
              type="button"
              round
              dense
              icon="mdi-content-copy"
              aria-label="Copy manual setup key"
              @click="copyTotpSecret"
            />
          </template>
        </q-input>
      </div>

      <SegmentedAuthCodeInput
        ref="totpCodeInput"
        v-model="totpCode"
        variant="outlined"
        label="6 digit code"
        :length="6"
        :disable="busy"
      />

      <q-btn
        unelevated
        color="primary"
        type="submit"
        icon-right="mdi-shield-check"
        label="Verify code"
        :disable="busy || totpCode.length !== 6"
        :loading="busy"
        no-caps
      />
    </template>

    <template v-else>
      <q-banner rounded class="bg-positive text-white">
        Account security setup is complete.
      </q-banner>

      <q-btn
        unelevated
        color="primary"
        type="submit"
        icon-right="mdi-check"
        label="Finish registration"
        :loading="busy"
        no-caps
      />
    </template>

    <div
      class="row items-center justify-center q-gutter-x-sm text-body2 text-grey-5"
    >
      <span>Already registered?</span>
      <q-btn
        flat
        dense
        type="button"
        color="grey-4"
        label="Sign in"
        no-caps
        :to="{ name: 'public-login', query: { email } }"
      />
    </div>
  </q-form>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import {
  beginOpaqueRegistration,
  finishOpaqueRegistration
} from "../../../opaque-ts/src";
import SegmentedAuthCodeInput from "@/components/SegmentedAuthCodeInput.vue";
import { apiPostJson } from "@/lib/api";
import { createQrCode } from "@/lib/qr-code";

type RegisterStartResponse = {
  registrationToken: string;
  nextStep: string;
};

type RegisterPasswordStartResponse = {
  registrationToken: string;
  nextStep: string;
  serverKeyId: string;
  serverPublicKeyBase64: string;
  evaluatedElementBase64: string;
};

type RegisterPasswordResponse = {
  registrationToken: string;
  nextStep: string;
};

type RegisterTotpResponse = {
  registrationToken: string;
  nextStep: string;
};

type RegisterCompleteResponse = {
  nextStep: string;
  user: {
    id: string;
    email: string;
    displayName: string | null;
    status: string;
  };
};

type FocusableInput = {
  focus: () => void;
};

const props = withDefaults(
  defineProps<{
    initialEmail?: string;
  }>(),
  {
    initialEmail: ""
  }
);

const emit = defineEmits<{
  registered: [response: RegisterCompleteResponse];
}>();

const confirmPassword = ref("");
const confirmPasswordTouched = ref(false);
const displayName = ref("");
const email = ref(props.initialEmail);
const emailTouched = ref(false);
const emailInput = ref<FocusableInput | null>(null);
const errorMessage = ref("");
const password = ref("");
const passwordInput = ref<FocusableInput | null>(null);
const registrationToken = ref("");
const statusMessage = ref("");
const step = ref<"account" | "passwordSetup" | "totp" | "complete">("account");
const busy = ref(false);
const totpCode = ref("");
const totpCodeInput = ref<FocusableInput | null>(null);
const totpSecret = ref("");

const totpIssuer = "Template App";
const normalizedEmail = computed(() => email.value.trim());
const normalizedTotpSecret = computed(() =>
  totpSecret.value.replaceAll(" ", "")
);
const isEmailValid = computed(
  () => normalizedEmail.value.length >= 3 && normalizedEmail.value.includes("@")
);
const totpSetupUri = computed(() => {
  const issuer = encodeURIComponent(totpIssuer);
  const account = encodeURIComponent(normalizedEmail.value);

  return `otpauth://totp/${issuer}:${account}?secret=${normalizedTotpSecret.value}&issuer=${issuer}&algorithm=SHA1&digits=6&period=30`;
});
const totpQrCode = computed(() => {
  if (normalizedTotpSecret.value.length === 0) {
    return null;
  }

  try {
    return createQrCode(totpSetupUri.value, {
      border: 4,
      errorCorrectionLevel: "medium"
    });
  } catch {
    return null;
  }
});
const emailErrorMessage = computed(() =>
  normalizedEmail.value.length === 0
    ? "Required"
    : isEmailValid.value
      ? ""
      : "Enter an email with at least 3 characters and @."
);
const showEmailError = computed(
  () => emailTouched.value && emailErrorMessage.value.length > 0
);
const passwordReady = computed(
  () => password.value.length > 0 && password.value === confirmPassword.value
);
const showPasswordMismatch = computed(
  () =>
    confirmPasswordTouched.value &&
    confirmPassword.value.length > 0 &&
    password.value !== confirmPassword.value
);

watch(
  () => props.initialEmail,
  value => {
    if (step.value === "account") {
      email.value = value;
    }
  }
);

watch(step, () => {
  void focusActiveInput();
});

onMounted(() => {
  void focusActiveInput();
});

async function handleSubmit() {
  errorMessage.value = "";
  statusMessage.value = "";

  if (step.value === "account") {
    await startRegistration();
    return;
  }

  if (step.value === "passwordSetup") {
    await setupPassword();
    return;
  }

  if (step.value === "totp") {
    await setupTotp();
    return;
  }

  await completeRegistration();
}

async function startRegistration() {
  emailTouched.value = true;

  if (!isEmailValid.value) {
    return;
  }

  busy.value = true;

  try {
    const response = await apiPostJson<RegisterStartResponse>(
      "/auth/register",
      {
        displayName: displayName.value.trim() || null,
        email: normalizedEmail.value
      }
    );

    registrationToken.value = response.registrationToken;
    step.value = "passwordSetup";
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function setupPassword() {
  if (!passwordReady.value) {
    confirmPasswordTouched.value = true;
    return;
  }

  busy.value = true;

  try {
    const opaqueStart = await beginOpaqueRegistration(password.value);
    const opaqueResponse = await apiPostJson<RegisterPasswordStartResponse>(
      "/auth/register/password/start",
      {
        blindedElementBase64: opaqueStart.request.blindedElementBase64,
        registrationToken: registrationToken.value
      }
    );
    const opaqueRegistration = await finishOpaqueRegistration(
      opaqueStart.state,
      opaqueResponse
    );
    const response = await apiPostJson<RegisterPasswordResponse>(
      "/auth/register/password",
      {
        opaqueRegistrationRecordJson: opaqueRegistration.registrationRecordJson,
        registrationToken: opaqueResponse.registrationToken
      }
    );

    registrationToken.value = response.registrationToken;
    password.value = "";
    confirmPassword.value = "";
    totpCode.value = "";
    totpSecret.value = createTotpSecret();
    step.value = "totp";
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function setupTotp() {
  busy.value = true;

  try {
    const response = await apiPostJson<RegisterTotpResponse>(
      "/auth/register/totp",
      {
        code: totpCode.value,
        registrationToken: registrationToken.value,
        totpSecret: totpSecret.value
      }
    );

    registrationToken.value = response.registrationToken;
    totpSecret.value = "";
    totpCode.value = "";
    step.value = "complete";
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function completeRegistration() {
  busy.value = true;

  try {
    const response = await apiPostJson<RegisterCompleteResponse>(
      "/auth/register/complete",
      {
        displayName: displayName.value.trim() || null,
        registrationToken: registrationToken.value
      }
    );

    emit("registered", response);
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function copyTotpSecret() {
  try {
    await navigator.clipboard.writeText(totpSecret.value);
    statusMessage.value = "Setup key copied.";
  } catch {
    errorMessage.value = "Clipboard access failed.";
  }
}

function createTotpSecret() {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  const bytes = crypto.getRandomValues(new Uint8Array(20));
  let bits = 0;
  let bitCount = 0;
  let output = "";

  for (const byte of bytes) {
    bits = (bits << 8) | byte;
    bitCount += 8;

    while (bitCount >= 5) {
      output += alphabet[(bits >> (bitCount - 5)) & 31];
      bitCount -= 5;
    }
  }

  if (bitCount > 0) {
    output += alphabet[(bits << (5 - bitCount)) & 31];
  }

  return output.replace(/(.{4})/g, "$1 ").trim();
}

async function focusActiveInput() {
  await nextTick();

  const input =
    step.value === "account"
      ? emailInput.value
      : step.value === "passwordSetup"
        ? passwordInput.value
        : step.value === "totp"
          ? totpCodeInput.value
          : null;

  input?.focus();
}

function handleError(error: unknown) {
  errorMessage.value =
    error instanceof Error ? error.message : "Registration failed.";
}
</script>

<style scoped lang="scss">
.totp-setup {
  display: grid;
  grid-template-columns: minmax(9rem, 10rem) minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}

.totp-qr {
  display: grid;
  place-items: center;
  width: 10rem;
  aspect-ratio: 1;
  padding: 0.625rem;
  border: 1px solid rgba(255, 255, 255, 0.18);
  border-radius: 8px;
  background: #fff;
}

.totp-qr__image {
  display: block;
  width: 100%;
  height: 100%;
}

@media (max-width: 599px) {
  .totp-setup {
    grid-template-columns: 1fr;
  }

  .totp-qr {
    justify-self: center;
    width: min(68vw, 12rem);
  }
}
</style>
