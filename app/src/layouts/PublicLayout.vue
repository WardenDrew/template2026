<template>
  <q-layout view="hHh lpR fFf">
    <q-page-container>
      <q-page class="flex flex-center q-pa-md">
        <div class="column items-center full-width">
          <q-card flat bordered class="full-width" style="max-width: 448px">
            <q-card-section class="q-pa-lg">
              <div class="row items-center no-wrap q-gutter-x-md">
                <div class="column col">
                  <div class="text-subtitle2 text-weight-bold text-primary">
                    Template App
                  </div>

                  <div class="text-h5 text-weight-medium"> Account Access </div>
                </div>

                <q-btn
                  v-if="showBack"
                  flat
                  round
                  dense
                  icon="mdi-arrow-left"
                  :to="{ name: 'public-login' }"
                  aria-label="Back"
                />
              </div>
            </q-card-section>

            <q-card-section class="q-px-lg q-pt-none q-pb-lg">
              <router-view />
            </q-card-section>
          </q-card>

          <div class="column items-center q-mt-md text-body2 text-grey-5">
            <div>API response: {{ apiResponseLabel }}</div>
          </div>
        </div>
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";

type HealthResponse = {
  status?: string;
};

const route = useRoute();
const apiResponseMillis = ref<number | null>(null);
const apiResponseUnavailable = ref(false);

const showBack = computed(
  () => route.name !== "public-login" && route.meta.hidePublicBack !== true
);
const apiResponseLabel = computed(() => {
  if (apiResponseMillis.value !== null) {
    return `${apiResponseMillis.value} ms`;
  }

  return apiResponseUnavailable.value ? "unavailable" : "checking";
});

onMounted(() => {
  void refreshApiResponseTime();
});

async function refreshApiResponseTime() {
  apiResponseUnavailable.value = false;
  const startedAt = performance.now();

  try {
    const response = await fetch("/api/healthz", { cache: "no-store" });

    if (!response.ok) {
      throw new Error(`Health check failed with status ${response.status}.`);
    }

    const health = (await response.json()) as HealthResponse;

    if (health.status !== "ok") {
      throw new Error("Health check response was invalid.");
    }

    apiResponseMillis.value = Math.max(
      1,
      Math.round(performance.now() - startedAt)
    );
  } catch {
    apiResponseMillis.value = null;
    apiResponseUnavailable.value = true;
  }
}
</script>
