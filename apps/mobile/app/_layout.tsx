/**
 * Root layout: providers (safe area, session, offline sync), the biometric
 * gate, and the root navigator that switches between (auth) and (tabs).
 */
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import {
  authenticateGate,
  isBiometricLockEnabled,
  type GateResult,
} from '../lib/biometric';
import { AuthProvider, useAuth } from '../lib/auth';
import { SyncProvider } from '../lib/offline-queue';
import { colors, fontSize, fontWeight, radius, space } from '../lib/theme';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <StatusBar style="light" />
      <AuthProvider>
        <SyncProvider>
          <BiometricGate>
            <RootNavigator />
          </BiometricGate>
        </SyncProvider>
      </AuthProvider>
    </SafeAreaProvider>
  );
}

function RootNavigator() {
  const { status } = useAuth();
  return (
    <Stack
      screenOptions={{
        headerShown: false,
        contentStyle: { backgroundColor: colors.base },
      }}
    >
      <Stack.Screen name="index" />
      {status === 'signedIn' ? (
        <Stack.Screen name="(tabs)" />
      ) : (
        <Stack.Screen name="(auth)" />
      )}
    </Stack>
  );
}

/**
 * If the biometric lock is enabled, the app requires a successful
 * LocalAuthentication prompt (biometric first, platform passcode fallback)
 * before rendering anything beyond this gate.
 */
function BiometricGate({ children }: { children: ReactNode }) {
  const [state, setState] = useState<'checking' | 'locked' | 'open'>('checking');
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      const enabled = await isBiometricLockEnabled();
      if (!active) return;
      setState(enabled ? 'locked' : 'open');
    })();
    return () => {
      active = false;
    };
  }, []);

  const unlock = async () => {
    const result: GateResult = await authenticateGate();
    if (result.success) {
      setState('open');
    } else {
      setMessage(result.message ?? 'Authentication failed. Try again.');
    }
  };

  if (state === 'checking') {
    return <View style={styles.gate} />;
  }
  if (state === 'locked') {
    return (
      <View style={styles.gate}>
        <Ionicons name="lock-closed" size={48} color={colors.gold} />
        <Text style={styles.gateTitle}>Mintmark is locked</Text>
        <Text style={styles.gateHint}>
          Use biometrics or your device passcode to unlock your collection.
        </Text>
        <Pressable style={styles.unlockButton} onPress={() => void unlock()}>
          <Text style={styles.unlockLabel}>Unlock</Text>
        </Pressable>
        {message ? <Text style={styles.gateError}>{message}</Text> : null}
      </View>
    );
  }
  return <>{children}</>;
}

const styles = StyleSheet.create({
  gate: {
    flex: 1,
    backgroundColor: colors.base,
    alignItems: 'center',
    justifyContent: 'center',
    padding: space[6],
    gap: space[3],
  },
  gateTitle: {
    color: colors.text,
    fontSize: fontSize.xl,
    fontWeight: fontWeight.bold,
  },
  gateHint: {
    color: colors.textMuted,
    fontSize: fontSize.sm,
    textAlign: 'center',
  },
  gateError: { color: colors.negative, fontSize: fontSize.sm },
  unlockButton: {
    backgroundColor: colors.gold,
    borderRadius: radius.md,
    paddingVertical: space[3],
    paddingHorizontal: space[8],
    marginTop: space[2],
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
  },
  unlockLabel: {
    color: colors.base,
    fontSize: fontSize.base,
    fontWeight: fontWeight.semibold,
  },
});
