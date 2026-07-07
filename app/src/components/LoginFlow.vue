<template>
  <q-form class="column q-gutter-y-md" @submit.prevent="handleSubmit">
    <q-banner v-if="errorMessage" rounded class="bg-negative text-white">
      {{ errorMessage }}
    </q-banner>

    <q-input
      v-if="step === 'email'"
      ref="emailInput"
      v-model="email"
      outlined
      type="email"
      label="Email"
      autocomplete="email"
      clearable
      :disable="busy"
      :error="showEmailError"
      :error-message="emailErrorMessage"
      @blur="emailTouched = true"
      @clear="clearEmail"
    />

    <template v-else-if="step === 'password'">
      <div class="text-body2 text-grey-5">
        Enter your password. It is checked from this device, and the password
        never leaves this device.
      </div>

      <q-input
        ref="passwordInput"
        v-model="password"
        outlined
        type="password"
        label="Password"
        autocomplete="current-password"
        :disable="busy"
      />
    </template>

    <template v-else>
      <div class="text-body2 text-grey-5">
        Enter the current 6 digit code from your authenticator app.
      </div>

      <SegmentedAuthCodeInput
        ref="totpInput"
        v-model="totpCode"
        variant="outlined"
        label="TOTP code"
        :length="6"
        :disable="busy"
      />
    </template>

    <q-btn
      unelevated
      color="primary"
      type="submit"
      icon="mdi-login"
      :label="primaryActionLabel"
      :disable="primaryActionDisabled"
      :loading="busy"
      no-caps
    />

    <div
      v-if="step === 'email'"
      class="row items-center justify-center q-gutter-x-sm text-body2 text-grey-5"
    >
      <span>No account?</span>
      <q-btn
        flat
        dense
        type="button"
        color="grey-4"
        label="Create one"
        no-caps
        :to="{ name: 'public-register', query: { email: normalizedEmail } }"
      />
    </div>

    <div
      v-if="step !== 'email'"
      class="column items-center q-gutter-y-xs text-body2 text-grey-5 q-pt-sm"
    >
      <div>Signing in as: {{ normalizedEmail }}</div>

      <q-btn
        flat
        type="button"
        dense
        color="primary"
        label="Start over"
        :disable="busy"
        no-caps
        @click="reset"
      />
    </div>
  </q-form>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import { beginOpaqueLogin, finishOpaqueLogin } from "../../../opaque-ts/src";
import SegmentedAuthCodeInput from "@/components/SegmentedAuthCodeInput.vue";
import { apiPostJson } from "@/lib/api";

type LoginStartResponse = {
  loginToken: string;
  nextStep: string;
};

type LoginOpaqueStartResponse = {
  loginToken: string;
  nextStep: string;
  serverKeyId: string;
  evaluatedElementBase64: string;
  serverPublicKeyBase64: string;
  clientPublicKeyBase64: string;
  envelopeNonceBase64: string;
  envelopeCiphertextBase64: string;
  serverNonceBase64: string;
  serverEphemeralPublicKeyBase64: string;
};

type LoginOpaqueFinishResponse = {
  loginToken: string;
  nextStep: string;
};

type LoginTotpVerifyResponse = {
  loginToken: string;
  nextStep: string;
};

type LoginUser = {
  id: string;
  email: string;
  displayName: string | null;
  status: string;
};

type LoginCompleteResponse = {
  accessToken: string;
  expiresAt: string;
  scopes: string[];
  user: LoginUser;
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
  authenticated: [response: LoginCompleteResponse];
}>();

const email = ref<string | null>(props.initialEmail);
const emailTouched = ref(false);
const errorMessage = ref("");
const emailInput = ref<FocusableInput | null>(null);
const loginToken = ref("");
const password = ref("");
const passwordInput = ref<FocusableInput | null>(null);
const step = ref<"email" | "password" | "totp">("email");
const busy = ref(false);
const totpCode = ref("");
const totpInput = ref<FocusableInput | null>(null);

const normalizedEmail = computed(() => (email.value ?? "").trim());
const isEmailValid = computed(
  () => normalizedEmail.value.length >= 3 && normalizedEmail.value.includes("@")
);
const emailErrorMessage = computed(() =>
  normalizedEmail.value.length === 0
    ? "Required"
    : isEmailValid.value
      ? ""
      : "Enter an email with at least 3 characters and @."
);
const showEmailError = computed(
  () =>
    step.value === "email" &&
    emailTouched.value &&
    emailErrorMessage.value.length > 0
);
const primaryActionDisabled = computed(
  () =>
    busy.value ||
    (step.value === "password" && password.value.length === 0) ||
    (step.value === "totp" && totpCode.value.length !== 6)
);
const primaryActionLabel = computed(() =>
  step.value === "email"
    ? "Continue"
    : step.value === "totp"
      ? "Verify"
      : "Sign in"
);

watch(
  () => props.initialEmail,
  value => {
    if (step.value === "email") {
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

  if (step.value === "email") {
    await startLogin();
    return;
  }

  if (step.value === "password") {
    await verifyPassword();
    return;
  }

  await verifyTotpAndCompleteLogin();
}

async function startLogin() {
  emailTouched.value = true;

  if (!isEmailValid.value) {
    return;
  }

  busy.value = true;

  try {
    const response = await apiPostJson<LoginStartResponse>("/auth/login", {
      email: normalizedEmail.value,
      scopes: [".default"]
    });

    loginToken.value = response.loginToken;
    password.value = "";
    totpCode.value = "";
    step.value = "password";
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function verifyPassword() {
  busy.value = true;

  try {
    const opaqueStart = await beginOpaqueLogin(password.value);
    const opaqueStartResponse = await apiPostJson<LoginOpaqueStartResponse>(
      "/auth/login/opaque/start",
      {
        blindedElementBase64: opaqueStart.request.blindedElementBase64,
        loginToken: loginToken.value
      }
    );
    const opaqueFinish = await finishOpaqueLogin(
      opaqueStart.state,
      opaqueStartResponse
    );
    const opaqueFinishResponse = await apiPostJson<LoginOpaqueFinishResponse>(
      "/auth/login/opaque/finish",
      {
        ...opaqueFinish.request,
        loginToken: opaqueStartResponse.loginToken
      }
    );

    loginToken.value = opaqueFinishResponse.loginToken;

    if (opaqueFinishResponse.nextStep === "verifyTotp") {
      password.value = "";
      totpCode.value = "";
      step.value = "totp";
      return;
    }

    await completeLogin(opaqueFinishResponse.loginToken);
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function verifyTotpAndCompleteLogin() {
  busy.value = true;

  try {
    const totpResponse = await apiPostJson<LoginTotpVerifyResponse>(
      "/auth/login/totp/verify",
      {
        code: totpCode.value,
        loginToken: loginToken.value
      }
    );

    loginToken.value = totpResponse.loginToken;
    await completeLogin(totpResponse.loginToken);
  } catch (error) {
    handleError(error);
  } finally {
    busy.value = false;
  }
}

async function completeLogin(verifiedLoginToken: string) {
  const completeResponse = await apiPostJson<LoginCompleteResponse>(
    "/auth/login/complete",
    {
      loginToken: verifiedLoginToken
    }
  );

  password.value = "";
  emit("authenticated", completeResponse);
}

function reset() {
  errorMessage.value = "";
  loginToken.value = "";
  step.value = "email";
  password.value = "";
  totpCode.value = "";
}

function clearEmail() {
  email.value = "";
  emailTouched.value = false;
}

async function focusActiveInput() {
  await nextTick();

  const input =
    step.value === "email"
      ? emailInput.value
      : step.value === "password"
        ? passwordInput.value
        : totpInput.value;

  input?.focus();
}

function handleError(error: unknown) {
  errorMessage.value = error instanceof Error ? error.message : "Login failed.";
}
</script>
