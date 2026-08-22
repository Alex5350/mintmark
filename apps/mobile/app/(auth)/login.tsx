import { Link } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, StyleSheet, Text, View } from 'react-native';
import { Screen } from '../../components/Screen';
import { Button, Field } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { colors, fontSize, fontWeight, space } from '../../lib/theme';

export default function LoginScreen() {
  const { signIn } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!email.trim() || !password) {
      setError('Enter your email and password.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await signIn(email.trim(), password);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Sign-in failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen title="Mintmark" subtitle="Sign in to your collection">
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
            placeholder="••••••••"
            secureTextEntry
          />
          {error ? <Text style={styles.error}>{error}</Text> : null}
          <Button label="Sign in" onPress={() => void submit()} loading={busy} />
          <Link href="/(auth)/register" style={styles.switchLink}>
            <Text style={styles.switchText}>
              New here? <Text style={styles.switchBold}>Create an account</Text>
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
