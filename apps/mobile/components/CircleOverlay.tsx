/**
 * Circular alignment overlay for the guided coin capture: a centered ring
 * with crosshair guides the collector to fill the circle with the coin
 * before shooting the obverse, then the reverse.
 */
import { StyleSheet, View } from 'react-native';
import { colors } from '../lib/theme';

const RING_SIZE = 260;

export function CircleOverlay() {
  return (
    <View style={styles.wrap} pointerEvents="none">
      {/* Dim everything outside the ring, keep the circle bright. */}
      <View style={styles.dim} />
      <View style={styles.ring}>
        <View style={styles.brightCircle} />
        <View style={styles.crossH} />
        <View style={styles.crossV} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { ...StyleSheet.absoluteFill, alignItems: 'center', justifyContent: 'center' },
  dim: {
    ...StyleSheet.absoluteFill,
    backgroundColor: 'rgba(14, 17, 22, 0.72)',
  },
  ring: {
    width: RING_SIZE,
    height: RING_SIZE,
    borderRadius: RING_SIZE / 2,
    borderWidth: 2,
    borderColor: colors.gold,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  brightCircle: {
    width: RING_SIZE - 4,
    height: RING_SIZE - 4,
    borderRadius: (RING_SIZE - 4) / 2,
    backgroundColor: 'rgba(227, 197, 92, 0.06)',
  },
  crossH: {
    position: 'absolute',
    width: 24,
    height: StyleSheet.hairlineWidth,
    backgroundColor: colors.goldSoft,
    opacity: 0.7,
  },
  crossV: {
    position: 'absolute',
    width: StyleSheet.hairlineWidth,
    height: 24,
    backgroundColor: colors.goldSoft,
    opacity: 0.7,
  },
});
