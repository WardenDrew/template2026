<template>
  <div class="column q-gutter-y-xs">
    <div v-if="label" class="text-caption text-grey-7">
      {{ label }}
    </div>

    <div class="row items-center no-wrap q-col-gutter-x-xs">
      <template v-for="index in length" :key="index">
        <div v-if="showsSeparatorBefore(index - 1)" class="col-auto text-h6">
          {{ separator }}
        </div>

        <div class="col">
          <q-input
            :ref="input => setInputRef(input, index - 1)"
            :model-value="digitAt(index - 1)"
            :outlined="variant === 'outlined'"
            :filled="variant === 'filled'"
            dense
            maxlength="1"
            inputmode="numeric"
            :autocomplete="index === 1 ? autocomplete : 'off'"
            :aria-label="createDigitLabel(index - 1)"
            :disable="disable"
            input-class="text-center text-body1"
            @focus="selectInputText"
            @keydown="handleKeydown($event, index - 1)"
            @paste.prevent="handlePaste($event, index - 1)"
            @update:model-value="value => handleInputValue(value, index - 1)"
          />
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { nextTick, ref, watch, type ComponentPublicInstance } from "vue";

type FocusTarget = ComponentPublicInstance & {
  focus?: () => void;
};

const props = withDefaults(
  defineProps<{
    autocomplete?: string;
    disable?: boolean;
    groupSize?: number;
    label?: string;
    length: number;
    modelValue: string;
    separator?: string;
    variant?: "filled" | "outlined";
  }>(),
  {
    autocomplete: "one-time-code",
    disable: false,
    groupSize: 0,
    label: "",
    separator: "-",
    variant: "outlined"
  }
);

const emit = defineEmits<{
  "update:modelValue": [value: string];
}>();

const inputRefs = ref<Array<FocusTarget | null>>([]);
const digitValues = ref(createDigits(props.modelValue));

watch(
  () => props.modelValue,
  value => {
    const normalizedValue = normalizeDigits(value);

    if (normalizedValue !== digitValues.value.join("")) {
      digitValues.value = createDigits(value);
    }
  }
);

function digitAt(index: number) {
  return digitValues.value[index] ?? "";
}

function showsSeparatorBefore(index: number) {
  return (
    index > 0 &&
    props.groupSize > 0 &&
    props.separator.length > 0 &&
    index % props.groupSize === 0
  );
}

function setInputRef(
  input: Element | ComponentPublicInstance | null,
  index: number
) {
  inputRefs.value[index] =
    input !== null && "focus" in input ? (input as FocusTarget) : null;
}

function handleInputValue(value: string | number | null, index: number) {
  const digits = normalizeDigits(String(value ?? ""));

  if (digits.length === 0) {
    updateDigit(index, "");
    return;
  }

  applyDigits(index, digits);
}

function handleKeydown(event: Event, index: number) {
  const keyboardEvent = event as KeyboardEvent;

  if (keyboardEvent.ctrlKey || keyboardEvent.metaKey || keyboardEvent.altKey) {
    return;
  }

  if (/^\d$/.test(keyboardEvent.key)) {
    keyboardEvent.preventDefault();
    applyDigits(index, keyboardEvent.key);
    return;
  }

  if (keyboardEvent.key === "Backspace") {
    keyboardEvent.preventDefault();
    deleteBackward(index);
    return;
  }

  if (keyboardEvent.key === "Delete") {
    keyboardEvent.preventDefault();
    updateDigit(index, "");
    return;
  }

  if (keyboardEvent.key === "ArrowLeft") {
    keyboardEvent.preventDefault();
    focusIndex(index - 1);
    return;
  }

  if (keyboardEvent.key === "ArrowRight") {
    keyboardEvent.preventDefault();
    focusIndex(index + 1);
    return;
  }

  if (
    keyboardEvent.key === "Tab" ||
    keyboardEvent.key === "Enter" ||
    keyboardEvent.key === "Home" ||
    keyboardEvent.key === "End"
  ) {
    return;
  }

  keyboardEvent.preventDefault();
}

function handlePaste(event: Event, index: number) {
  const clipboardEvent = event as ClipboardEvent;
  const pastedDigits = normalizeDigits(
    clipboardEvent.clipboardData?.getData("text") ?? ""
  );

  if (pastedDigits.length === 0) {
    return;
  }

  applyDigits(index, pastedDigits);
}

function deleteBackward(index: number) {
  const digits = toDigitArray();

  if (digits[index]) {
    digits[index] = "";
    emitDigits(digits);
    return;
  }

  if (index === 0) {
    return;
  }

  digits[index - 1] = "";
  emitDigits(digits);
  focusIndex(index - 1);
}

function updateDigit(index: number, digit: string) {
  const digits = toDigitArray();

  digits[index] = digit;
  emitDigits(digits);
}

function applyDigits(startIndex: number, digitsToApply: string) {
  const digits = toDigitArray();
  let targetIndex = startIndex;

  for (const digit of digitsToApply) {
    if (targetIndex >= props.length) {
      break;
    }

    digits[targetIndex] = digit;
    targetIndex += 1;
  }

  emitDigits(digits);
  focusIndex(Math.min(targetIndex, props.length - 1));
}

function emitDigits(digits: string[]) {
  digitValues.value = digits;
  emit("update:modelValue", digits.join(""));
}

function toDigitArray() {
  return Array.from({ length: props.length }, (_, index) => digitAt(index));
}

async function focusIndex(index: number) {
  if (props.disable) {
    return;
  }

  const normalizedIndex = Math.min(Math.max(index, 0), props.length - 1);

  await nextTick();
  inputRefs.value[normalizedIndex]?.focus?.();
}

function focus() {
  const firstEmptyIndex = toDigitArray().findIndex(digit => digit.length === 0);

  void focusIndex(firstEmptyIndex === -1 ? 0 : firstEmptyIndex);
}

function selectInputText(event: Event) {
  if (event.target instanceof HTMLInputElement) {
    event.target.select();
  }
}

function createDigitLabel(index: number) {
  const prefix = props.label || "Authentication code";

  return `${prefix} digit ${index + 1} of ${props.length}`;
}

function normalizeDigits(value: string) {
  return value.replace(/\D/g, "").slice(0, props.length);
}

function createDigits(value: string) {
  const normalizedValue = normalizeDigits(value);

  return Array.from(
    { length: props.length },
    (_, index) => normalizedValue[index] ?? ""
  );
}

defineExpose({
  focus
});
</script>
