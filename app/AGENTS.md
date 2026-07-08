# GUI Instructions

- Prefer the clean current-state UI shape. Do not add backwards-compatible
  duplicate routes, compatibility state adapters, or unused feature flags unless
  explicitly requested.
- Build mobile first, then use Quasar responsive utilities for larger app
  viewports.
- Prefer Quasar components, props, spacing classes, color utilities, grid
  classes, and responsive helpers over custom CSS.
- Keep custom CSS for app UI out of page/component files. Put theme-wide changes
  in `src/css/theme.scss`.
- Use Yarn for Quasar projects configured in `node_modules` mode.
- Never patch or manually modify `node_modules/`, `.quasar/`, `dist/`, or other
  generated dependency/build output folders.
- Reusable UI lives under `src/components/` and should not assume whether it is
  rendered in a page, card, drawer, or dialog.
- Layouts own app shell concerns: navigation drawers, headers, breadcrumbs,
  route titles, and route subtitles.
- Pages own route-level loading, error, empty, and primary action states.
- Domain API helpers live under `src/lib/` and expose typed request/response
  functions.
- Keep same-origin API calls behind the shared API wrapper and route them
  through `/api`.
- Use explicit route records with route names and metadata. Do not rely on
  filename-based routing.
- Use route metadata for shell display concerns such as `title`, `subtitle`,
  `breadcrumb`, `backTo`, and header visibility.
- Redirect authentication/session failures consistently from page-level loading
  code.
- Reset dialog/workflow local state when the dialog closes. Load prerequisites
  when it opens.
- Any automatic work triggered by keypress/input events must be debounced,
  including API calls, parent updates, validation beyond local field state,
  persistence, search/filtering, and expensive computation.
- Use icon buttons for compact commands and include accessible labels/tooltips.
- Prefer `q-banner`, `q-spinner`, `q-dialog`, `q-stepper`, `q-list`, `q-card`,
  and Quasar grid utilities for standard app workflows before adding custom
  primitives.
- Do not build marketing or landing-page screens for application tasks. The
  first screen should be the usable workflow unless explicitly requested.
- OPAQUE auth and TOTP setup are part of this template. Do not copy
  project-encryption unlock, key-generation, key-wrapping, recovery, or
  encrypted-record UI into this template unless the target product explicitly
  requires it.
- Run `yarn lint:check`, `yarn typecheck`, and `yarn build` for GUI changes.
- Do not start or leave `yarn dev`/`quasar dev` running as a handoff step.
  Human browser testing uses Docker Compose from the project root.
