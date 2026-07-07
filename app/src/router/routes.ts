import type { RouteRecordRaw } from "vue-router";

// /api/* is reserved for the same-origin reverse proxy.
const routes = [
  {
    path: "/",
    redirect: { name: "public-login" }
  },
  {
    path: "/app/pub",
    component: () => import("@/layouts/PublicLayout.vue"),
    redirect: { name: "public-login" },
    children: [
      {
        path: "login",
        name: "public-login",
        component: () => import("@/pages/public/LoginPage.vue"),
        meta: {
          title: "Sign in"
        }
      },
      {
        path: "register",
        name: "public-register",
        component: () => import("@/pages/public/RegisterPage.vue"),
        meta: {
          hidePublicBack: false,
          title: "Create account"
        }
      }
    ]
  },
  {
    path: "/app/main",
    component: () => import("@/layouts/MainLayout.vue"),
    redirect: { name: "main-dashboard" },
    meta: {
      breadcrumb: "Main"
    },
    children: [
      {
        path: "dashboard",
        name: "main-dashboard",
        component: () => import("@/pages/main/DashboardPage.vue"),
        meta: {
          breadcrumb: "Dashboard",
          subtitle: "Status, recent activity, and quick actions",
          title: "Dashboard"
        }
      },
      {
        path: "resources",
        name: "main-resources",
        component: () => import("@/pages/main/ResourcePage.vue"),
        meta: {
          breadcrumb: "Resources",
          subtitle: "Create, open, and manage resources",
          title: "Resources"
        }
      }
    ]
  },
  {
    path: "/:pathMatch(.*)*",
    component: () => import("@/layouts/PublicLayout.vue"),
    children: [
      {
        path: "",
        name: "not-found",
        component: () => import("@/pages/NotFoundPage.vue"),
        meta: {
          hidePageHeader: true,
          title: "Page not found"
        }
      }
    ]
  }
] satisfies RouteRecordRaw[];

export default routes;
