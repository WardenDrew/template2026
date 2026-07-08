# GUI Template

This template captures the reusable Quasar/Vue app shape:

- Quasar CLI with Vite, Vue 3, TypeScript strict mode, Pinia, and Vue Router.
- Explicit route definitions instead of filename-based routing.
- Same-origin API calls through `/api`.
- Generic OPAQUE login/signup and TOTP setup/verification flows.
- Layout-owned navigation, route titles, subtitles, and breadcrumbs.
- Typed domain API clients and reusable client utilities in `src/lib/`.
- Reusable workflow components in `src/components/`.
- Page components that own loading, error, empty, and success states.
- Theme-only SCSS customization.

## Folder Layout

```text
app/
  package.json
  quasar.config.ts
  src-capacitor/
    capacitor.config.ts
    android/
  src/
    App.vue
    css/
      app.scss
      quasar.variables.scss
      theme.scss
    layouts/
      MainLayout.vue
      PublicLayout.vue
    router/
      index.ts
      routes.ts
      route-meta.d.ts
    lib/
      api.ts
      main-navigation.ts
      qr-code.ts
      resource-api.ts
    pages/
      main/
      public/
    components/
      LoginFlow.vue
      RegistrationFlow.vue
      SegmentedAuthCodeInput.vue
      resources/
```

## Routing

Use a hand-authored route table:

- Reserve `/api/*` for the reverse proxy.
- Put public routes under a public layout.
- Put authenticated app routes under a main layout.
- Use route names for navigation and redirects.
- Store page shell metadata in `route.meta`: `title`, `subtitle`,
  `breadcrumb`, `backTo`, and header visibility flags.

See `src/router/routes.ts`, `src/router/index.ts`, and
`src/router/route-meta.d.ts`.

## Layouts

Layouts own shell concerns:

- Responsive drawers.
- Header/footer navigation.
- Breadcrumbs from matched route metadata.
- Route title/subtitle rendering.
- Top-level navigation visibility based on access state.

Pages should not repeat app shell markup.

See `src/layouts/MainLayout.vue`.

## API Client

Keep a small shared fetch wrapper:

- Prefix every app API call with `/api`.
- Set JSON headers consistently.
- Attach auth/session headers in one place.
- Normalize API and validation errors into a single `ApiError`.

Then create one typed domain file per bounded area, such as
`src/lib/resource-api.ts`.

See `src/lib/api.ts` and `src/lib/resource-api.ts`.

## Client Utilities

Keep reusable browser-safe utilities in `src/lib/`.

- `qr-code.ts` generates QR code matrices and render-ready SVG path/viewBox
  data for arbitrary byte payloads. It supports QR versions 1-40, configurable
  error correction, and no runtime package dependency.
- Prefer rendering the returned `path` in an inline SVG when the caller needs
  styling control, or `renderQrCodeSvg()` when a complete SVG string is useful.

## Pages And Components

Pages own route-level behavior:

- Load route data in `onMounted()` or a focused watcher.
- Show `q-spinner` while loading.
- Show `q-banner` for error and empty states.
- Redirect to logout or login for authentication failures.
- Delegate reusable create/edit flows to components.

Reusable dialogs and flows:

- Use `v-model` with `update:modelValue`.
- Load prerequisites when opened.
- Reset local state when closed.
- Emit domain events such as `created`, `saved`, and `done`.

See `src/pages/main/ResourcePage.vue`,
`src/components/resources/ResourceCreateDialog.vue`, and
`src/components/resources/ResourceDetailsStep.vue`.

## Styling

- Put shared palette variables and global Quasar primitive overrides in
  `src/css/theme.scss`.
- Import the theme from both `app.scss` and `quasar.variables.scss`.
- Avoid component-scoped CSS unless the component has a real layout need that
  Quasar utilities cannot express.

See `src/css/theme.scss`.

## Commands

```sh
corepack enable
yarn install --immutable
yarn lint:check
yarn typecheck
yarn build
yarn build:android
```

Capacitor Android builds follow Quasar's Capacitor mode. The Docker wrapper in
the repository root provides the Android SDK/JDK environment and then runs:

```sh
yarn quasar build -m capacitor -T android
```

Quasar writes web assets into `src-capacitor/www`, syncs the native Android
project, and places build output under `dist/capacitor`. Use
`../scripts/build-android-apk.sh` from this app folder, or
`./scripts/build-android-apk.sh` from the repository root, to publish the APK to
root `appdist/`.
