# Android Builder

This image is only the Android build environment. Quasar remains the source of
truth for Capacitor builds.

The root script builds this image, mounts the repository, runs:

```sh
yarn quasar build -m capacitor -T android
```

and copies Quasar's APK output from `app/dist/capacitor` to
`appdist/<app-package-name>-<app-version>.apk`.

The image installs Node 24, JDK 21, Android command-line tools, platform tools,
Android SDK 36, and Android build tools 36 plus 35. These versions match the
generated Capacitor Android project under `app/src-capacitor/android` and the
build-tools version requested by its Capacitor dependencies.
