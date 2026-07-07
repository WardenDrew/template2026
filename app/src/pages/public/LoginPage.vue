<template>
  <LoginFlow
    :initial-email="initialEmail"
    @authenticated="handleAuthenticated"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import LoginFlow from "@/components/LoginFlow.vue";

type LoginCompleteResponse = {
  accessToken: string;
  expiresAt: string;
  scopes: string[];
  user: {
    id: string;
    email: string;
    displayName: string | null;
    status: string;
  };
};

const accessTokenStorageKey = "template.accessToken";
const route = useRoute();
const router = useRouter();

const initialEmail = computed(() =>
  typeof route.query.email === "string" ? route.query.email : ""
);

async function handleAuthenticated(response: LoginCompleteResponse) {
  window.localStorage.setItem(accessTokenStorageKey, response.accessToken);

  await router.push(getRedirectPath());
}

function getRedirectPath() {
  if (
    typeof route.query.redirect === "string" &&
    route.query.redirect.startsWith("/app/main")
  ) {
    return route.query.redirect;
  }

  return "/app/main/dashboard";
}
</script>
