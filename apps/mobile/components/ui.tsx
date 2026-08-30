/** Small shared dark-theme primitives (cards, badges, buttons, fields). */
import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { colors, fontSize, fontWeight, radius, space, tabular } from '../lib/theme';

// --- text ------------------------------------------------------------------

export function Muted({ children }: { children: ReactNode }) {
  return <Text style={mutedStyles.text}>{children}</Text>;
}

const mutedStyles = StyleSheet.create({
  text: { color: colors.textMuted, fontSize: fontSize.sm },
});

/** Numeric text with tabular figures so digits align while prices tick. */
export function Num({
  children,
  color,
  size = fontSize.base,
}: {
  children: ReactNode;
  color?: string;
  size?: number;
}) {
  return (
    <Text style={[numStyles.text, tabular, { color: color ?? colors.text, fontSize: size }]}>
      {children}
    </Text>
  );
}

const numStyles = StyleSheet.create({
  text: { fontWeight: fontWeight.medium },
});

// --- card ------------------------------------------------------------------

export function Card({ children, style }: { children: ReactNode; style?: object }) {
  return <View style={[cardStyles.card, style]}>{children}</View>;
}

const cardStyles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: StyleSheet.hairlineWidth,
    borderRadius: radius.md,
    padding: space[4],
  },
});

// --- badge -----------------------------------------------------------------

export function Badge({
  label,
  color = colors.warning,
}: {
  label: string;
  color?: string;
}) {
  return (
    <View style={[badgeStyles.badge, { borderColor: color }]}>
      <Text style={[badgeStyles.label, { color }]}>{label}</Text>
    </View>
  );
}

const badgeStyles = StyleSheet.create({
  badge: {
    borderWidth: 1,
    borderRadius: radius.full,
    paddingVertical: 2,
    paddingHorizontal: space[2],
  },
  label: { fontSize: fontSize.xs, fontWeight: fontWeight.semibold, ...tabular },
});

// --- button ----------------------------------------------------------------

type ButtonVariant = 'primary' | 'ghost' | 'destructive';

export function Button({
  label,
  onPress,
  variant = 'primary',
  disabled = false,
  loading = false,
}: {
  label: string;
  onPress: () => void;
  variant?: ButtonVariant;
  disabled?: boolean;
  loading?: boolean;
}) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled || loading}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: disabled || loading, busy: loading }}
      style={({ pressed }) => [
        buttonStyles.base,
        variant === 'primary' && buttonStyles.primary,
        variant === 'ghost' && buttonStyles.ghost,
        variant === 'destructive' && buttonStyles.destructive,
        (disabled || loading) && buttonStyles.disabled,
        pressed && { opacity: 0.85 },
      ]}
    >
      {loading ? (
        <ActivityIndicator color={variant === 'primary' ? colors.base : colors.text} />
      ) : (
        <Text
          style={[
            buttonStyles.label,
            variant === 'primary' && { color: colors.base },
            variant === 'ghost' && { color: colors.focus },
            variant === 'destructive' && { color: colors.negative },
          ]}
        >
          {label}
        </Text>
      )}
    </Pressable>
  );
}

const buttonStyles = StyleSheet.create({
  base: {
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: radius.md,
    paddingVertical: space[3],
    paddingHorizontal: space[4],
    minHeight: 48,
  },
  primary: { backgroundColor: colors.gold },
  ghost: { backgroundColor: 'transparent', borderColor: colors.border, borderWidth: 1 },
  destructive: {
    backgroundColor: 'transparent',
    borderColor: colors.negative,
    borderWidth: 1,
  },
  disabled: { opacity: 0.5 },
  label: { fontSize: fontSize.base, fontWeight: fontWeight.semibold },
});

// --- field -----------------------------------------------------------------

export function Field({
  label,
  value,
  onChangeText,
  placeholder,
  secureTextEntry = false,
  autoCapitalize = 'none',
  keyboardType = 'default',
}: {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  placeholder?: string;
  secureTextEntry?: boolean;
  autoCapitalize?: 'none' | 'sentences';
  keyboardType?: 'default' | 'email-address';
}) {
  return (
    <View style={fieldStyles.wrap}>
      <Text style={fieldStyles.label}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={colors.textMuted}
        secureTextEntry={secureTextEntry}
        autoCapitalize={autoCapitalize}
        autoCorrect={false}
        keyboardType={keyboardType}
        style={fieldStyles.input}
        accessibilityLabel={label}
        textContentType={secureTextEntry ? 'password' : 'emailAddress'}
      />
    </View>
  );
}

const fieldStyles = StyleSheet.create({
  wrap: { gap: space[1] },
  label: {
    color: colors.textMuted,
    fontSize: fontSize.sm,
    fontWeight: fontWeight.medium,
  },
  input: {
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.md,
    color: colors.text,
    fontSize: fontSize.base,
    paddingHorizontal: space[3],
    minHeight: 48,
  },
});
