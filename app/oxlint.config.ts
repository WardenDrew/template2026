import { defineConfig } from "oxlint";

export default defineConfig({
  $schema: "./node_modules/oxlint/configuration_schema.json",
  ignorePatterns: [
    "**/node_modules/",
    ".yarn/",
    "dist/",
    "quasar.config.*.temporary.compiled*",
    ".quasar/"
  ],
  options: {
    typeAware: true,
    typeCheck: true,
    maxWarnings: 10
  },
  plugins: ["typescript", "vue", "import", "eslint", "promise", "unicorn"],
  categories: {
    correctness: "error"
  },
  rules: {},
  env: {
    builtin: true
  }
});
