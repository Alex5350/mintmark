/**
 * Expo app config (app.json's programmatic replacement).
 *
 * The API base URL is environment-driven so a release build never ships the
 * dev loopback: `API_BASE_URL` (or `EXPO_PUBLIC_API_URL`) at build/start
 * time wins; development falls back to IPv4 loopback, which the iOS
 * simulator requires (its sandbox resolves `localhost` to IPv6 `::1` first
 * and cannot open IPv6 loopback sockets). Release builds without an
 * explicit URL get an explicit, loud placeholder rather than a silent
 * wrong default.
 */
import type { ExpoConfig } from 'expo/config';

const envBaseUrl = process.env.API_BASE_URL ?? process.env.EXPO_PUBLIC_API_URL;

const config: ExpoConfig = {
  name: 'Mintmark',
  slug: 'mobile',
  version: '1.0.0',
  scheme: 'mintmark',
  orientation: 'portrait',
  icon: './assets/icon.png',
  userInterfaceStyle: 'dark',
  ios: {
    supportsTablet: true,
  },
  android: {
    adaptiveIcon: {
      backgroundColor: '#0E1116',
      foregroundImage: './assets/android-icon-foreground.png',
      backgroundImage: './assets/android-icon-background.png',
      monochromeImage: './assets/android-icon-monochrome.png',
    },
    predictiveBackGestureEnabled: false,
  },
  web: {
    favicon: './assets/favicon.png',
  },
  plugins: [
    'expo-router',
    [
      'expo-secure-store',
      {
        faceIDPermission: 'Mintmark uses Face ID to unlock your collection.',
      },
    ],
    [
      'expo-image-picker',
      {
        cameraPermission:
          'Mintmark uses the camera to photograph coins for identification.',
        photosPermission:
          'Mintmark lets you pick coin photos from your library.',
      },
    ],
    'expo-font',
  ],
  extra: {
    apiBaseUrl:
      envBaseUrl ??
      (process.env.NODE_ENV === 'production'
        ? 'https://SET-API_BASE_URL-BEFORE-BUILDING.example'
        : 'http://127.0.0.1:5100'),
  },
};

export default config;
