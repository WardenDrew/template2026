import type { RouteLocationRaw } from "vue-router";

declare module "vue-router" {
  interface RouteMeta {
    backTo?: RouteLocationRaw;
    breadcrumb?: string;
    hidePageHeader?: boolean;
    hidePublicBack?: boolean;
    subtitle?: string;
    title?: string;
  }
}
