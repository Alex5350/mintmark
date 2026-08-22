/**
 * Biometric gate helpers (expo-local-authentication).
 *
 * `authenticateGate` leaves the device passcode fallback enabled, so users
 * without enrolled biometrics still unlock with their platform passcode —
 * the graceful fallback required by the scaffold spec.
 */
import * as LocalAuthentication from 'expo-local-authentication';
import * as SecureStore from 'expo-secure-store';

const BIOMETRIC_LOCK_KEY = 'mintmark.biometricLock';

export interface BiometricAvailability {
  hasHardware: boolean;
  enrolled: boolean;
  /** Safe to offer the lock toggle only when both are true. */
  available: boolean;
  biometrics: string[];
}

export async function getBiometricAvailability(): Promise<BiometricAvailability> {
  const [hasHardware, enrolled, types] = await Promise.all([
    LocalAuthentication.hasHardwareAsync(),
    LocalAuthentication.isEnrolledAsync(),
    LocalAuthentication.supportedAuthenticationTypesAsync(),
  ]);
  const nameFor = (type: number): string => {
    switch (type) {
      case LocalAuthentication.AuthenticationType.FINGERPRINT:
        return 'Fingerprint';
      case LocalAuthentication.AuthenticationType.FACIAL_RECOGNITION:
        return 'Face';
      case LocalAuthentication.AuthenticationType.IRIS:
        return 'Iris';
      default:
        return 'Biometric';
    }
  };
  return {
    hasHardware,
    enrolled,
    available: hasHardware && enrolled,
    biometrics: types.map(nameFor),
  };
}

export async function isBiometricLockEnabled(): Promise<boolean> {
  const value = await SecureStore.getItemAsync(BIOMETRIC_LOCK_KEY);
  return value === '1';
}

export async function setBiometricLockEnabled(enabled: boolean): Promise<void> {
  if (enabled) {
    await SecureStore.setItemAsync(BIOMETRIC_LOCK_KEY, '1');
  } else {
    await SecureStore.deleteItemAsync(BIOMETRIC_LOCK_KEY);
  }
}

export interface GateResult {
  success: boolean;
  /** Human-readable reason when the gate did not open. */
  message?: string;
}

/** Runs the platform authentication prompt (biometric with passcode fallback). */
export async function authenticateGate(
  promptMessage = 'Unlock Mintmark',
): Promise<GateResult> {
  try {
    const result = await LocalAuthentication.authenticateAsync({
      promptMessage,
      cancelLabel: 'Cancel',
      disableDeviceFallback: false, // platform passcode fallback stays available
    });
    if (result.success) return { success: true };
    // LocalAuthenticationError is a string union in SDK 57
    // ('user_fallback' | 'user_cancel' | ...).
    return {
      success: false,
      message:
        result.error === 'user_fallback' || result.error === 'user_cancel'
          ? 'Unlock cancelled.'
          : 'Authentication failed. Try again.',
    };
  } catch {
    return { success: false, message: 'Authentication is unavailable.' };
  }
}
