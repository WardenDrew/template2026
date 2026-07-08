#!/usr/bin/env bash
set -euo pipefail

repo_root="${REPO_ROOT:-/workspace}"
app_dir="${repo_root}/app"
output_dir="${repo_root}/appdist"
build_type="${ANDROID_BUILD_TYPE:-release}"

case "${build_type}" in
  release)
    quasar_build_flags=()
    ;;
  debug)
    quasar_build_flags=(--debug)
    ;;
  *)
    echo "ANDROID_BUILD_TYPE must be 'release' or 'debug'." >&2
    exit 1
    ;;
esac

cd "${app_dir}"

yarn install --immutable

if [ ! -d src-capacitor ]; then
  echo "Missing app/src-capacitor. Run Quasar Capacitor mode setup before building Android." >&2
  exit 1
fi

if [ -f src-capacitor/yarn.lock ]; then
  (
    cd src-capacitor
    yarn install --immutable
  )
fi

gradle_args=(--no-daemon "$@")
yarn quasar build -m capacitor -T android "${quasar_build_flags[@]}" -- "${gradle_args[@]}"

artifact_name="$(
  node --input-type=module <<'NODE'
import { readFileSync } from 'node:fs';

const pkg = JSON.parse(readFileSync('package.json', 'utf8'));
const rawName = pkg.name || pkg.productName || 'app';
const name = rawName
  .toLowerCase()
  .replace(/[^a-z0-9._-]+/g, '-')
  .replace(/^-+|-+$/g, '') || 'app';
const version = String(pkg.version || '0.0.0').replace(/[^a-zA-Z0-9._-]+/g, '-');

process.stdout.write(`${name}-${version}.apk`);
NODE
)"

apk_search_root="${app_dir}/dist/capacitor/android"
if [ ! -d "${apk_search_root}" ]; then
  echo "Quasar did not create ${apk_search_root}." >&2
  exit 1
fi

mapfile -t apks < <(
  find "${apk_search_root}" -type f -name "*.apk" \
    | sort \
    | grep "/apk/${build_type}/" || true
)

if [ "${#apks[@]}" -eq 0 ]; then
  mapfile -t apks < <(find "${apk_search_root}" -type f -name "*.apk" | sort)
fi

if [ "${#apks[@]}" -eq 0 ]; then
  echo "No APK was found under ${apk_search_root}." >&2
  exit 1
fi

mkdir -p "${output_dir}"
cp "${apks[0]}" "${output_dir}/${artifact_name}"

echo "Published ${output_dir}/${artifact_name}"
