<template>
  <div class="column q-gutter-md">
    <q-banner v-if="errorMessage" rounded class="bg-negative text-white">
      {{ errorMessage }}
    </q-banner>

    <div v-if="loading" class="row justify-center q-py-xl">
      <q-spinner color="primary" size="48px" />
    </div>

    <template v-else>
      <q-card bordered flat>
        <q-card-section class="row items-center q-col-gutter-md">
          <div class="col-12 col-sm">
            <div class="text-subtitle2 text-grey-6">Signed in as</div>
            <div class="text-h6">
              {{ userLabel }}
            </div>
          </div>

          <div class="col-12 col-sm-auto">
            <q-btn
              unelevated
              color="primary"
              icon="mdi-folder-table"
              label="Open resources"
              :to="{ name: 'main-resources' }"
              no-caps
            />
          </div>
        </q-card-section>
      </q-card>

      <q-banner rounded class="bg-grey-10 text-grey-2">
        The template stack is running. Use Resources to exercise authenticated
        API calls through the same-origin `/api` proxy.
      </q-banner>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { ApiError, apiGetJson } from "@/lib/api";

type CurrentUser = {
  id: string;
  email: string;
  displayName: string | null;
  status: string;
};

const router = useRouter();
const errorMessage = ref("");
const loading = ref(false);
const user = ref<CurrentUser | null>(null);

const userLabel = computed(() => {
  if (user.value === null) {
    return "";
  }

  return user.value.displayName
    ? `${user.value.displayName} (${user.value.email})`
    : user.value.email;
});

onMounted(() => {
  void loadUser();
});

async function loadUser() {
  loading.value = true;
  errorMessage.value = "";

  try {
    user.value = await apiGetJson<CurrentUser>("/user/me");
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      await router.push({ name: "public-login" });
      return;
    }

    errorMessage.value =
      error instanceof Error ? error.message : "Dashboard failed to load.";
  } finally {
    loading.value = false;
  }
}
</script>
