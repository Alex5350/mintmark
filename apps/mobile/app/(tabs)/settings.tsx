/**
 * Settings: biometric lock toggle, account + sign out, offline-queue
 * readout (pending count, flush state, last error).
 */
import { useEffect, useState } from 'react';
import { StyleSheet, Switch, Text, View } from 'react-native';
import { Screen } from '../../components/Screen';
import { Button, Card, Muted } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import {
  getBiometricAvailability,
  setBiometricLockEnabled,
  type BiometricAvailability,
} from '../../lib/biometric';
import { flush, useSyncStatus } from '../../lib/offline-queue';
import { colors, fontSize, fontWeight, space } from '../../lib/theme';

export default function SettingsScreen() {
  const { user, signOut } = useAuth();
  const sync = useSyncStatus();
  const [availability, setAvailability] = useState<BiometricAvailability | null>(null);
  const [lockEnabled, setLockEnabled] = useState(false);

  useEffect(() => {
    void (async () => {
      setAvailability(await getBiometricAvailability());
    })();
  }, []);

  const toggleLock = async (value: boolean) => {
    // Enabling requires enrolled biometrics (or platform passcode); the
    // availability gate is rendered before this can fire.
    await setBiometricLockEnabled(value);
    setLockEnabled(value);
  };

  return (
    <Screen title="Settings" subtitle="Account, security, sync">
      <Card style={styles.card}>
        <Text style={styles.sectionTitle}>Account</Text>
        <Muted>Signed in as</Muted>
        <Text style={styles.email}>{user?.email ?? '—'}</Text>
        <Button label="Sign out" variant="destructive" onPress={() => void signOut()} />
      </Card>

      <Card style={styles.card}>
        <Text style={styles.sectionTitle}>Security</Text>
        <View style={styles.row}>
          <View style={styles.rowMain}>
            <Text style={styles.label}>Biometric lock</Text>
            {availability ? (
              <Muted>
                {availability.available
                  ? `Unlocks with ${availability.biometrics.join(' / ') || 'device passcode'}`
                  : 'No enrolled biometrics on this device'}
              </Muted>
            ) : (
              <Muted>Checking hardware…</Muted>
            )}
          </View>
          <Switch
            value={lockEnabled}
            onValueChange={(value) => void toggleLock(value)}
            disabled={!availability?.available}
            trackColor={{ true: colors.gold, false: colors.border }}
            thumbColor={lockEnabled ? colors.base : colors.textMuted}
          />
        </View>
        <Muted>When on, Mintmark asks for biometrics or the device passcode at launch.</Muted>
      </Card>

      <Card style={styles.card}>
        <Text style={styles.sectionTitle}>Offline sync</Text>
        <View style={styles.statRow}>
          <Text style={styles.label}>Pending mutations</Text>
          <Text style={[styles.value, { color: sync.pending > 0 ? colors.warning : colors.positive }]}>
            {sync.ready ? sync.pending : '…'}
          </Text>
        </View>
        <View style={styles.statRow}>
          <Text style={styles.label}>Flush</Text>
          <Text style={styles.value}>
            {sync.flushing ? 'running' : sync.lastFlushedAt ? 'idle' : 'waiting'}
          </Text>
        </View>
        <View style={styles.statRow}>
          <Text style={styles.label}>Last synced</Text>
          <Text style={styles.value}>
            {sync.lastFlushedAt ? new Date(sync.lastFlushedAt).toLocaleTimeString() : '—'}
          </Text>
        </View>
        {sync.lastError ? (
          <Text style={styles.syncError}>{sync.lastError}</Text>
        ) : null}
        <Button
          label={sync.flushing ? 'Flushing…' : 'Flush now'}
          variant="ghost"
          disabled={sync.flushing}
          onPress={() => void flush()}
        />
        <Muted>
          Failed mutations are stored durably and replayed with their idempotency keys.
        </Muted>
      </Card>
    </Screen>
  );
}

const styles = StyleSheet.create({
  card: { gap: space[2] },
  sectionTitle: {
    color: colors.text,
    fontSize: fontSize.lg,
    fontWeight: fontWeight.bold,
  },
  row: { flexDirection: 'row', alignItems: 'center', gap: space[3] },
  rowMain: { flex: 1, gap: 2 },
  label: { color: colors.text, fontSize: fontSize.base, fontWeight: fontWeight.medium },
  email: { color: colors.text, fontSize: fontSize.base },
  statRow: { flexDirection: 'row', justifyContent: 'space-between' },
  value: { color: colors.textMuted, fontSize: fontSize.base, fontWeight: fontWeight.medium },
  syncError: { color: colors.negative, fontSize: fontSize.sm },
});
