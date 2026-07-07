<template>
  <div class="column q-gutter-md">
    <div class="row items-center justify-end">
      <q-btn
        unelevated
        color="primary"
        icon="mdi-plus"
        label="Create Resource"
        no-caps
        @click="createDialogOpen = true"
      />
    </div>

    <q-banner v-if="errorMessage" rounded class="bg-negative text-white">
      {{ errorMessage }}
    </q-banner>

    <div v-if="loading" class="row justify-center q-py-xl">
      <q-spinner color="primary" size="48px" />
    </div>

    <q-banner
      v-else-if="resources.length === 0"
      rounded
      class="bg-grey-10 text-grey-2"
    >
      No resources yet.
    </q-banner>

    <div v-else class="row q-col-gutter-md">
      <div
        v-for="resource in resources"
        :key="resource.id"
        class="col-12 col-sm-6 col-lg-4"
      >
        <q-card bordered flat>
          <q-card-section class="column q-gutter-sm">
            <div class="text-h6">
              {{ resource.name }}
            </div>

            <div v-if="resource.description" class="text-body2 text-grey-6">
              {{ resource.description }}
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <ResourceCreateDialog
      v-model="createDialogOpen"
      @created="handleResourceCreated"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import ResourceCreateDialog from "@/components/resources/ResourceCreateDialog.vue";
import { ApiError } from "@/lib/api";
import { listResources, type Resource } from "@/lib/resource-api";

const router = useRouter();
const createDialogOpen = ref(false);
const errorMessage = ref("");
const loading = ref(false);
const resources = ref<Resource[]>([]);

onMounted(() => {
  void loadResources();
});

async function loadResources() {
  loading.value = true;
  errorMessage.value = "";

  try {
    resources.value = await listResources();
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      await router.push({ name: "public-login" });
      return;
    }

    errorMessage.value =
      error instanceof Error ? error.message : "Resources failed to load.";
  } finally {
    loading.value = false;
  }
}

function handleResourceCreated(resource: Resource) {
  resources.value = [
    resource,
    ...resources.value.filter(item => item.id !== resource.id)
  ].sort((left, right) => left.name.localeCompare(right.name));
}
</script>
