/**
 * Session context on top of the SecureStore token pair (lib/tokens.ts).
 *
 * Tokens themselves never enter React state — only presence does. The
 * signed-in user profile is cached in AsyncStorage, which is fine: it is
 * non-secret cache, and the rule is "tokens only in expo-secure-store".
 */
import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api, claimsFromToken, setUnauthorizedHandler, type User } from './api';
import { clearQueue } from './offline-queue';
import { clearTokens, getTokens, setTokens } from './tokens';

const USER_CACHE_KEY = 'mintmark.cachedUser';

export type AuthStatus = 'restoring' | 'signedOut' | 'signedIn';

interface AuthContextValue {
  status: AuthStatus;
  user: User | null;
  signIn: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

async function readCachedUser(): Promise<User | null> {
  try {
    const raw = await AsyncStorage.getItem(USER_CACHE_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  } catch {
    return null;
  }
}

async function cacheUser(user: User | null): Promise<void> {
  try {
    if (user) {
      await AsyncStorage.setItem(USER_CACHE_KEY, JSON.stringify(user));
    } else {
      await AsyncStorage.removeItem(USER_CACHE_KEY);
    }
  } catch {
    // Cache only — losing it costs nothing but a profile refetch.
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('restoring');
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      const tokens = await getTokens();
      if (!active) return;
      if (tokens) {
        const cached = await readCachedUser();
        if (cached) {
          setUser(cached);
        } else {
          const claims = claimsFromToken(tokens.accessToken);
          setUser({ id: claims.sub ?? '', email: claims.email ?? '' });
        }
        setStatus('signedIn');
      } else {
        setStatus('signedOut');
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const establish = useCallback(async (email: string, password: string, mode: 'login' | 'register') => {
    const response =
      mode === 'login'
        ? await api.auth.login({ email, password })
        : await api.auth.register({ email, password });
    await setTokens({
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
    });
    // The auth endpoints return tokens only (no profile object), so the
    // display identity comes from the access token's claims.
    const claims = claimsFromToken(response.accessToken);
    const user: User = { id: claims.sub ?? '', email: claims.email ?? email };
    await cacheUser(user);
    setUser(user);
    setStatus('signedIn');
  }, []);

  const signIn = useCallback(
    (email: string, password: string) => establish(email, password, 'login'),
    [establish],
  );

  const register = useCallback(
    (email: string, password: string) => establish(email, password, 'register'),
    [establish],
  );

  const signOut = useCallback(async () => {
    // Contain the session server-side first: revoke the refresh token's
    // family so extracted tokens stop working. Then drop local state —
    // including the offline queue, whose rows are not user-scoped and must
    // not replay under a different account.
    const tokens = await getTokens();
    if (tokens) {
      await api.auth.logout(tokens.refreshToken);
    }
    await clearTokens();
    await clearQueue();
    await cacheUser(null);
    setUser(null);
    setStatus('signedOut');
  }, []);

  // Any API surface 401ing even after the refresh-once retry ends the
  // session app-wide.
  useEffect(() => {
    setUnauthorizedHandler(() => void signOut());
    return () => setUnauthorizedHandler(null);
  }, [signOut]);

  const value = useMemo(
    () => ({ status, user, signIn, register, signOut }),
    [status, user, signIn, register, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>');
  return context;
}
