<template>
  <RegistrationFlow
    :initial-email="initialEmail"
    @registered="handleRegistered"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import RegistrationFlow from "@/components/RegistrationFlow.vue";

type RegisterCompleteResponse = {
  nextStep: string;
  user: {
    id: string;
    email: string;
    displayName: string | null;
    status: string;
  };
};

const route = useRoute();
const router = useRouter();

const initialEmail = computed(() =>
  typeof route.query.email === "string" ? route.query.email : ""
);

async function handleRegistered(response: RegisterCompleteResponse) {
  await router.push({
    name: "public-login",
    query: { email: response.user.email }
  });
}
</script>
