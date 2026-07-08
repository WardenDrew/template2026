<template>
  <q-layout view="lHr LpR lFr">
    <q-header bordered class="gt-sm bg-primary text-theme-dark">
      <q-toolbar>
        <q-btn
          flat
          dense
          aria-label="Toggle navigation"
          @click="toggleMainDrawer"
        >
          <q-icon :name="mainDrawerToggleIcon" class="q-px-xs" />
        </q-btn>

        <q-toolbar-title> Template App </q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-drawer
      v-model="mainDrawerOpen"
      side="left"
      bordered
      :behavior="mainDrawerBehavior"
      :elevated="mainDrawerOverlay"
      :overlay="mainDrawerOverlay"
      :width="280"
    >
      <q-list padding>
        <q-item-label header>
          <div class="row items-center no-wrap">
            <div class="col">Template App</div>

            <q-btn
              v-if="isDesktop"
              flat
              dense
              round
              :icon="mainDrawerPersistent ? 'mdi-pin' : 'mdi-pin-off'"
              :aria-label="
                mainDrawerPersistent
                  ? 'Disable persistent navigation'
                  : 'Keep navigation open'
              "
              @click="toggleMainDrawerPersistent"
            >
              <q-tooltip>
                {{
                  mainDrawerPersistent
                    ? "Disable persistent navigation"
                    : "Keep navigation open"
                }}
              </q-tooltip>
            </q-btn>
          </div>
        </q-item-label>

        <q-item
          v-for="navItem in visibleNavigationItems"
          :key="navItem.label"
          v-ripple
          clickable
          exact
          :active="isNavigationItemActive(navItem)"
          active-class="text-primary"
          :to="navItem.to"
        >
          <q-item-section avatar>
            <q-icon :name="navItem.icon" />
          </q-item-section>

          <q-item-section>
            {{ navItem.label }}
          </q-item-section>
        </q-item>

        <q-separator spaced />

        <q-item v-ripple clickable @click="logout">
          <q-item-section avatar>
            <q-icon name="mdi-logout" />
          </q-item-section>

          <q-item-section> Logout </q-item-section>
        </q-item>
      </q-list>
    </q-drawer>

    <q-page-container>
      <q-page class="q-pa-md">
        <div class="column q-gutter-y-md">
          <div v-if="showPageHeader" class="column q-gutter-y-sm">
            <q-breadcrumbs v-if="breadcrumbs.length > 0" class="text-grey-6">
              <q-breadcrumbs-el
                v-for="breadcrumb in breadcrumbs"
                :key="breadcrumb.label"
                :label="breadcrumb.label"
                :to="breadcrumb.to"
              />
            </q-breadcrumbs>

            <div class="row items-start q-gutter-sm no-wrap">
              <q-btn
                v-if="route.meta.backTo"
                flat
                round
                dense
                icon="mdi-arrow-left"
                :to="route.meta.backTo"
                aria-label="Back"
              />

              <div class="column q-gutter-y-xs">
                <div class="text-h5 text-weight-medium">
                  {{ pageTitle }}
                </div>

                <div v-if="pageSubtitle" class="text-body2 text-grey-6">
                  {{ pageSubtitle }}
                </div>
              </div>
            </div>
          </div>

          <router-view />
        </div>
      </q-page>
    </q-page-container>

    <q-footer elevated class="lt-md bg-grey-10 text-white">
      <q-toolbar class="q-px-xs">
        <q-btn
          flat
          dense
          aria-label="Toggle navigation"
          @click="toggleMainDrawer"
        >
          <q-icon :name="mainDrawerToggleIcon" class="q-px-xs" />
        </q-btn>

        <q-btn
          flat
          dense
          round
          icon="mdi-home"
          :to="{ name: 'main-dashboard' }"
          aria-label="Home"
        />
      </q-toolbar>
    </q-footer>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useQuasar } from "quasar";
import { useRoute, useRouter, type RouteLocationRaw } from "vue-router";
import { apiPostJson } from "@/lib/api";
import {
  hasAnyScope,
  mainUserNavigationItems,
  readAccessTokenScopes,
  type MainNavigationItem
} from "@/lib/main-navigation";

const $q = useQuasar();
const route = useRoute();
const router = useRouter();
const mainDrawerOpen = ref(false);
const mainDrawerPersistent = ref(true);

const isDesktop = computed(() => $q.screen.gt.sm);
const mainDrawerOverlay = computed(
  () => !(isDesktop.value && mainDrawerPersistent.value)
);
const mainDrawerBehavior = computed(() =>
  mainDrawerOverlay.value ? "mobile" : "desktop"
);
const mainDrawerToggleIcon = computed(() =>
  mainDrawerOpen.value ? "mdi-menu-open" : "mdi-menu-close"
);
const pageTitle = computed(() => route.meta.title ?? "");
const pageSubtitle = computed(() => route.meta.subtitle ?? "");
const showPageHeader = computed(
  () => !route.meta.hidePageHeader && pageTitle.value.length > 0
);
const accessTokenScopes = computed(() => new Set(readAccessTokenScopes()));
const visibleNavigationItems = computed(() =>
  mainUserNavigationItems.filter(navItem =>
    hasAnyScope(accessTokenScopes.value, navItem.requiredScopes)
  )
);
const breadcrumbs = computed(() =>
  route.matched
    .filter(routeRecord => routeRecord.meta.breadcrumb !== undefined)
    .map(routeRecord => ({
      label: routeRecord.meta.breadcrumb ?? "",
      to:
        routeRecord.name === undefined
          ? undefined
          : ({ name: routeRecord.name } satisfies RouteLocationRaw)
    }))
);

watch(
  [isDesktop, mainDrawerPersistent],
  ([desktop, persistent]) => {
    if (!desktop) {
      mainDrawerOpen.value = false;
      return;
    }

    if (persistent) {
      mainDrawerOpen.value = true;
    }
  },
  { immediate: true }
);

function toggleMainDrawer() {
  mainDrawerOpen.value = !mainDrawerOpen.value;
}

function toggleMainDrawerPersistent() {
  mainDrawerPersistent.value = !mainDrawerPersistent.value;

  if (isDesktop.value && mainDrawerPersistent.value) {
    mainDrawerOpen.value = true;
  }
}

function isNavigationItemActive(navItem: MainNavigationItem) {
  if (navItem.activeRouteNames?.includes(String(route.name))) {
    return true;
  }

  return (
    typeof navItem.to === "object" &&
    "name" in navItem.to &&
    navItem.to.name === route.name
  );
}

async function logout() {
  try {
    await apiPostJson<void>("/auth/logout");
  } catch {
    // Local token cleanup still matters if the server session already expired.
  }

  window.localStorage.removeItem("template.accessToken");
  await router.push({ name: "public-login" });
}
</script>
