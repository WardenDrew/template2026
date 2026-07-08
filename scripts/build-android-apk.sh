#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/.." && pwd)"
image_name="${ANDROID_BUILDER_IMAGE:-template2026-android-builder}"

docker build \
  --build-arg "BUILDER_UID=$(id -u)" \
  --build-arg "BUILDER_GID=$(id -g)" \
  --tag "${image_name}" \
  --file "${repo_root}/android-builder/Dockerfile" \
  "${repo_root}/android-builder"

mkdir -p "${repo_root}/appdist"

docker run --rm \
  --volume "${repo_root}:/workspace" \
  --workdir /workspace \
  --env "ANDROID_BUILD_TYPE=${ANDROID_BUILD_TYPE:-release}" \
  "${image_name}" \
  "$@"
