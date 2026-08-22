/**
 * The ONLY place auth tokens live: expo-secure-store (ADR 0005 — tokens
 * never touch AsyncStorage). AsyncStorage is reserved for non-secret cache.
 */
import * as SecureStore from 'expo-secure-store';

const ACCESS_TOKEN_KEY = 'mintmark.accessToken';
const REFRESH_TOKEN_KEY = 'mintmark.refreshToken';

const KEYCHAIN_OPTIONS = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
} as const;

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export async function getTokens(): Promise<AuthTokens | null> {
  const [accessToken, refreshToken] = await Promise.all([
    SecureStore.getItemAsync(ACCESS_TOKEN_KEY, KEYCHAIN_OPTIONS),
    SecureStore.getItemAsync(REFRESH_TOKEN_KEY, KEYCHAIN_OPTIONS),
  ]);
  if (!accessToken || !refreshToken) return null;
  return { accessToken, refreshToken };
}

export async function setTokens(tokens: AuthTokens): Promise<void> {
  await Promise.all([
    SecureStore.setItemAsync(
      ACCESS_TOKEN_KEY,
      tokens.accessToken,
      KEYCHAIN_OPTIONS,
    ),
    SecureStore.setItemAsync(
      REFRESH_TOKEN_KEY,
      tokens.refreshToken,
      KEYCHAIN_OPTIONS,
    ),
  ]);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY, KEYCHAIN_OPTIONS),
    SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY, KEYCHAIN_OPTIONS),
  ]);
}
