import { defineRouter } from "#q-app";
import {
  createMemoryHistory,
  createRouter,
  createWebHashHistory,
  createWebHistory
} from "vue-router";
import routes from "./routes";

const accessTokenStorageKey = "template.accessToken";

export default defineRouter(() => {
  const createHistory = import.meta.env.QUASAR_SERVER
    ? createMemoryHistory
    : import.meta.env.QUASAR_VUE_ROUTER_MODE === "history"
      ? createWebHistory
      : createWebHashHistory;

  const Router = createRouter({
    scrollBehavior: () => ({ left: 0, top: 0 }),
    routes,
    history: createHistory(import.meta.env.QUASAR_VUE_ROUTER_BASE)
  });

  Router.beforeEach(to => {
    if (import.meta.env.QUASAR_SERVER) {
      return true;
    }

    if (!isAuthenticatedAppRoute(to.path)) {
      return true;
    }

    if (window.localStorage.getItem(accessTokenStorageKey) !== null) {
      return true;
    }

    return {
      name: "public-login",
      query: { redirect: to.fullPath }
    };
  });

  return Router;
});

function isAuthenticatedAppRoute(path: string) {
  return path.startsWith("/app/main");
}
