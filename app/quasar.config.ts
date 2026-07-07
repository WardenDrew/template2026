import { defineConfig } from "#q-app";

export default defineConfig(() => ({
  boot: [],
  css: ["app.scss"],
  extras: ["roboto-font", "mdi-v7"],
  build: {
    typescript: {
      strict: true,
      vueShim: true
    },
    filenameBasedRouting: false,
    vueRouterMode: "history"
  },
  devServer: {
    open: false
  },
  framework: {
    config: {
      dark: true
    },
    iconSet: "mdi-v7",
    plugins: []
  },
  animations: []
}));
