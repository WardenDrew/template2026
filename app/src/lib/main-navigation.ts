import type { RouteLocationRaw } from "vue-router";

export type MainNavigationItem = {
  activeRouteNames?: string[];
  icon: string;
  label: string;
  requiredScopes?: string[];
  to: RouteLocationRaw;
};

export const mainUserNavigationItems: MainNavigationItem[] = [
  {
    icon: "mdi-view-dashboard",
    label: "Dashboard",
    to: { name: "main-dashboard" }
  },
  {
    icon: "mdi-folder-table",
    label: "Resources",
    to: { name: "main-resources" }
  }
];

export function readAccessTokenScopes() {
  if (typeof window === "undefined") {
    return [];
  }

  return parseAccessTokenScopes(
    window.localStorage.getItem("template.accessToken")
  );
}

export function hasAnyScope(
  availableScopes: ReadonlySet<string>,
  requiredScopes: readonly string[] = []
) {
  return (
    requiredScopes.length === 0 ||
    requiredScopes.some(scope => availableScopes.has(scope))
  );
}

function parseAccessTokenScopes(accessToken: string | null) {
  if (!accessToken) {
    return [];
  }

  const payloadPart = accessToken.split(".")[1];

  if (!payloadPart) {
    return [];
  }

  try {
    const payload = JSON.parse(decodeBase64Url(payloadPart)) as {
      scope?: string | string[];
    };

    return typeof payload.scope === "string"
      ? payload.scope.split(/\s+/).filter(scope => scope.length > 0)
      : Array.isArray(payload.scope)
        ? payload.scope.filter(scope => typeof scope === "string")
        : [];
  } catch {
    return [];
  }
}

function decodeBase64Url(value: string) {
  const normalized = value.replaceAll("-", "+").replaceAll("_", "/");
  const padded = normalized.padEnd(
    normalized.length + ((4 - (normalized.length % 4)) % 4),
    "="
  );

  return atob(padded);
}
