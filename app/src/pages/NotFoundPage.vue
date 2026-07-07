<template>
  <div class="column items-center q-gutter-md q-pa-xl text-center">
    <q-icon name="mdi-alert-circle-outline" size="56px" color="grey-6" />
    <div class="text-h5">Page not found</div>
    <q-btn
      unelevated
      color="primary"
      label="Go to sign in"
      :to="{ name: 'public-login' }"
      no-caps
    />
  </div>
</template>
