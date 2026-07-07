<template>
  <q-form @submit.prevent="completeStep">
    <div class="row q-col-gutter-lg">
      <div class="col-12">
        <q-input
          ref="nameInput"
          v-model="localDetails.name"
          filled
          label="Resource name"
          maxlength="200"
          :error="nameTouched && !isNameValid"
          error-message="Resource name is required."
          bottom-slots
          @blur="nameTouched = true"
        />
      </div>

      <div class="col-12">
        <q-input
          v-model="localDetails.description"
          filled
          type="textarea"
          label="Description"
          maxlength="1000"
          autogrow
          bottom-slots
        />
      </div>

      <div class="col-12">
        <div class="row q-col-gutter-sm justify-end">
          <div class="col-12 col-sm-auto">
            <q-btn
              flat
              class="full-width"
              color="primary"
              label="Cancel"
              :disable="busy"
              no-caps
              @click="emit('cancel')"
            />
          </div>

          <div class="col-12 col-sm-auto">
            <q-btn
              unelevated
              class="full-width"
              color="primary"
              type="submit"
              icon-right="mdi-check"
              label="Create"
              :disable="!isNameValid"
              :loading="busy"
              no-caps
            />
          </div>
        </div>
      </div>
    </div>
  </q-form>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import type { ResourceDetails } from "@/lib/resource-api";

type FocusableInput = {
  focus: () => void;
};

const props = defineProps<{
  busy: boolean;
  modelValue: ResourceDetails;
}>();

const emit = defineEmits<{
  cancel: [];
  complete: [];
  "update:modelValue": [value: ResourceDetails];
}>();

const localDetails = ref<ResourceDetails>({
  ...props.modelValue
});
const nameInput = ref<FocusableInput | null>(null);
const nameTouched = ref(false);

const isNameValid = computed(() => localDetails.value.name.trim().length > 0);

watch(stepResetKey, () => {
  localDetails.value = { ...props.modelValue };
  nameTouched.value = false;
});

onMounted(async () => {
  await nextTick();
  nameInput.value?.focus();
});

function completeStep() {
  nameTouched.value = true;

  if (!isNameValid.value) {
    return;
  }

  emit("update:modelValue", normalizeDetails(localDetails.value));
  emit("complete");
}

function normalizeDetails(details: ResourceDetails): ResourceDetails {
  return {
    description: normalizeOptional(details.description),
    name: details.name.trim()
  };
}

function normalizeOptional(value: string | null) {
  const normalized = value?.trim();

  return normalized ? normalized : null;
}

function stepResetKey() {
  return JSON.stringify(props.modelValue);
}
</script>
