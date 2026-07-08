FROM node:24-trixie-slim

ARG ANDROID_COMMAND_LINE_TOOLS_VERSION=14742923
ARG ANDROID_PLATFORM=android-36
ARG ANDROID_BUILD_TOOLS=36.0.0
ARG ANDROID_COMPAT_BUILD_TOOLS=35.0.0
ARG BUILDER_UID=1000
ARG BUILDER_GID=1000

ENV DEBIAN_FRONTEND=noninteractive
ENV ANDROID_HOME=/opt/android-sdk
ENV ANDROID_SDK_ROOT=/opt/android-sdk
ENV GRADLE_USER_HOME=/home/node/.gradle
ENV PATH=/opt/android-sdk/cmdline-tools/latest/bin:/opt/android-sdk/platform-tools:${PATH}

RUN apt-get update \
    && apt-get install --no-install-recommends -y \
        ca-certificates \
        git \
        openjdk-21-jdk-headless \
        unzip \
        wget \
    && rm -rf /var/lib/apt/lists/*

RUN corepack enable

RUN mkdir -p "${ANDROID_SDK_ROOT}/cmdline-tools" \
    && wget --quiet \
        "https://dl.google.com/android/repository/commandlinetools-linux-${ANDROID_COMMAND_LINE_TOOLS_VERSION}_latest.zip" \
        --output-document=/tmp/android-commandline-tools.zip \
    && unzip -q /tmp/android-commandline-tools.zip -d "${ANDROID_SDK_ROOT}/cmdline-tools" \
    && mv "${ANDROID_SDK_ROOT}/cmdline-tools/cmdline-tools" "${ANDROID_SDK_ROOT}/cmdline-tools/latest" \
    && rm /tmp/android-commandline-tools.zip \
    && yes | sdkmanager --licenses >/dev/null \
    && sdkmanager \
        "platform-tools" \
        "platforms;${ANDROID_PLATFORM}" \
        "build-tools;${ANDROID_BUILD_TOOLS}" \
        "build-tools;${ANDROID_COMPAT_BUILD_TOOLS}"

RUN if ! getent group "${BUILDER_GID}" >/dev/null; then groupmod --gid "${BUILDER_GID}" node; fi \
    && if [ "$(id -u node)" != "${BUILDER_UID}" ]; then usermod --uid "${BUILDER_UID}" node; fi \
    && usermod --gid "${BUILDER_GID}" node \
    && mkdir -p /workspace "${GRADLE_USER_HOME}" \
    && chown -R "${BUILDER_UID}:${BUILDER_GID}" /home/node /workspace

COPY --chown=${BUILDER_UID}:${BUILDER_GID} build-apk.sh /usr/local/bin/build-android-apk
RUN chmod +x /usr/local/bin/build-android-apk

WORKDIR /workspace
USER node

ENTRYPOINT ["build-android-apk"]
