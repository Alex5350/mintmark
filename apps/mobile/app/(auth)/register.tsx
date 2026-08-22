import { Link } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, StyleSheet, Text, View } from 'react-native';
import { Screen } from '../../components/Screen';
import { Button, Field, Muted } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { colors, fontSize, fontWeight, space } from '../../lib/theme';

export default function RegisterScreen() {
  const { register } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!email.trim() || !password) {
      setError('Enter an email and password.');
      return;
    }
    if (password.length < 12) {
      setError('Passwords need at least 12 characters.');
      return;
    }
    if (password !== confirm) {
      setError('Passwords do not match.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await register(email.trim(), password);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Registration failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen title="Create account" subtitle="Start your collection">
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <View style={styles.form}>
          <Field
            label="Email"
            value={email}
            onChangeText={setEmail}
            placeholder="you@example.com"
            keyboardType="email-address"
          />
          <Field
            label="Password"
            value={password}
            onChangeText={setPassword}
            placeholder="At least 12 characters"
            secureTextEntry
          />
          <Field
            label="Confirm password"
            value={confirm}
            onChangeText={setConfirm}
            placeholder="Repeat your password"
            secureTextEntry
          />
          <Muted>Tokens are stored in the device keystore — never in plain storage.</Muted>
          {error ? <Text style={styles.error}>{error}</Text> : null}
          <Button label="Create account" onPress={() => void submit()} loading={busy} />
          <Link href="/(auth)/login" style={styles.switchLink}>
            <Text style={styles.switchText}>
              Already registered? <Text style={styles.switchBold}>Sign in</Text>
            </Text>
          </Link>
        </View>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  form: { gap: space[3], justifyContent: 'center', flex: 1 },
  error: { color: colors.negative, fontSize: fontSize.sm },
  switchLink: { alignItems: 'center', minHeight: 44, justifyContent: 'center' },
  switchText: { color: colors.textMuted, fontSize: fontSize.sm },
  switchBold: { color: colors.focus, fontWeight: fontWeight.semibold },
});
