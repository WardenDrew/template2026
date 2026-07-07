<template>
  <q-dialog v-model="openModel" persistent full-width>
    <q-card class="full-width" style="max-width: 640px">
      <q-card-section class="row items-center no-wrap">
        <div class="col">
          <div class="text-h6">Create Resource</div>
        </div>

        <q-btn
          flat
          dense
          round
          icon="mdi-close"
          aria-label="Close"
          @click="openModel = false"
        />
      </q-card-section>

      <q-separator />

      <q-card-section>
        <q-banner v-if="errorMessage" rounded class="bg-negative text-white">
          {{ errorMessage }}
        </q-banner>

        <ResourceDetailsStep
          v-model="details"
          :busy="busy"
          @cancel="openModel = false"
          @complete="create"
        />
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import ResourceDetailsStep from "@/components/resources/ResourceDetailsStep.vue";
import {
  createResource,
  type Resource,
  type ResourceDetails
} from "@/lib/resource-api";

const props = defineProps<{
  modelValue: boolean;
}>();

const emit = defineEmits<{
  created: [resource: Resource];
  "update:modelValue": [value: boolean];
}>();

const busy = ref(false);
const errorMessage = ref("");
const details = ref<ResourceDetails>(createEmptyDetails());

const openModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit("update:modelValue", value)
});

watch(
  () => props.modelValue,
  value => {
    if (!value) {
      busy.value = false;
      errorMessage.value = "";
      details.value = createEmptyDetails();
    }
  }
);

async function create() {
  busy.value = true;
  errorMessage.value = "";

  try {
    const resource = await createResource(details.value);

    emit("created", resource);
    openModel.value = false;
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : "Resource creation failed.";
  } finally {
    busy.value = false;
  }
}

function createEmptyDetails(): ResourceDetails {
  return {
    description: null,
    name: ""
  };
}
</script>
