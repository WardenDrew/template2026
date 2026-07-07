import { defineConfig } from "oxfmt";

export default defineConfig({
  $schema: "./node_modules/oxfmt/configuration_schema.json",
  ignorePatterns: [
    "**/node_modules/",
    ".yarn/",
    "dist/",
    "quasar.config.*.temporary.compiled*",
    ".quasar/"
  ],
  printWidth: 80,
  arrowParens: "avoid",
  bracketSpacing: true,
  bracketSameLine: false,
  htmlWhitespaceSensitivity: "strict",
  semi: true,
  singleQuote: false,
  quoteProps: "as-needed",
  trailingComma: "none",
  useTabs: false,
  vueIndentScriptAndStyle: false
});
