/**
 * Holding detail: identity + quantities, live valuation (melt, premium,
 * collectible, confidence band), the rules-engine premium factors with their
 * rationales, price provenance, and — for catalog rows — obverse/reverse
 * reference imagery.
 */
import { Ionicons } from '@expo/vector-icons';
import { Link, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Screen } from '../../../components/Screen';
import { Card, Muted, Num } from '../../../components/ui';
import {
  api,
  type CoinTypeDetail,
  type HoldingDetail,
  type Valuation,
} from '../../../lib/api';
import { itemFormLabel, knownMetal, metalLabel } from '../../../lib/enums';
import { colors, fontSize, fontWeight, metalColor, radius, space } from '../../../lib/theme';

/**
 * Presigned image URLs point at MinIO on the dev host as `localhost`, which
 * the iOS simulator sandbox resolves to broken IPv6 loopback — the same trap
 * as the API base URL, so normalize to IPv4 before display.
 */
function normalizeImageUrl(url: string): string {
  return url.replace('://localhost:', '://127.0.0.1:');
}

export default function HoldingDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const holdingId = Number(id);
  const [holding, setHolding] = useState<HoldingDetail | null>(null);
  const [valuation, setValuation] = useState<Valuation | null>(null);
  const [coinType, setCoinType] = useState<CoinTypeDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [detail, valuationResult] = await Promise.all([
          api.holdings.get(holdingId),
          api.holdings.valuation(holdingId),
        ]);
        if (!active) return;
        setHolding(detail);
        setValuation(valuationResult);
        if (detail.coinTypeId != null) {
          const type = await api.catalog
            .coinType(detail.coinTypeId)
            .catch(() => null);
          if (active) setCoinType(type);
        }
      } catch (cause) {
        if (active) {
          setError(cause instanceof Error ? cause.message : 'Could not load holding.');
        }
      }
    })();
    return () => {
      active = false;
    };
  }, [holdingId]);

  const metal = knownMetal(coinType?.detail.metal ?? null);
  const accent = metal ? metalColor(metal) : colors.gold;

  return (
    <Screen title="Holding" subtitle={holding?.displayName}>
      <ScrollView contentContainerStyle={styles.content}>
        <Link href="/(tabs)/collection" asChild>
          <Pressable hitSlop={8} style={styles.back}>
            <Ionicons name="chevron-back" size={16} color={colors.focus} />
            <Text style={styles.backLabel}>Collection</Text>
          </Pressable>
        </Link>
        {error ? <Text style={styles.error}>{error}</Text> : null}
        {!holding && !error ? <Muted>Loading holding…</Muted> : null}

        {holding ? (
          <>
            {coinType && (coinType.obverseImageUrl || coinType.reverseImageUrl) ? (
              <View style={styles.imageRow}>
                <CoinImage label="obverse" url={coinType.obverseImageUrl} />
                <CoinImage label="reverse" url={coinType.reverseImageUrl} />
              </View>
            ) : null}

            <Card style={styles.card}>
              <Text style={styles.name}>{holding.displayName}</Text>
              <Muted>
                {[
                  metalLabel(coinType?.detail.metal ?? null),
                  itemFormLabel(holding.form),
                ].join(' · ')}
              </Muted>
              <View style={styles.statRow}>
                <Stat label="Quantity" value={String(holding.effectiveQuantity)} />
                <Stat
                  label="Paid / unit"
                  value={
                    holding.effectivePurchasePricePerUnit
                      ? formatMoney(holding.effectivePurchasePricePerUnit.amount)
                      : '—'
                  }
                />
                <Stat
                  label="Acquired"
                  value={new Date(holding.purchasedAtUtc).toLocaleDateString('en-US', {
                    dateStyle: 'medium',
                  })}
                />
              </View>
            </Card>
          </>
        ) : null}

        {valuation ? (
          <>
            <Card style={styles.card}>
              <Muted>Collectible value</Muted>
              <Num size={fontSize['2xl']} color={accent}>
                {formatMoney(valuation.collectible.amount)}
              </Num>
              <Muted>
                range{' '}
                {formatMoney(valuation.confidenceBand.lowValue.amount)} –{' '}
                {formatMoney(valuation.confidenceBand.highValue.amount)}
              </Muted>
              <View style={styles.statRow}>
                <Stat label="Melt" value={formatMoney(valuation.melt.amount)} />
                <Stat label="Premium" value={formatMoney(valuation.premium.amount)} />
                <Stat
                  label="Multiplier"
                  value={`×${valuation.premiumMultiplier.toFixed(2)}`}
                />
              </View>
            </Card>

            <Card style={styles.card}>
              <Text style={styles.sectionTitle}>Premium factors</Text>
              {valuation.premiumFactors.map((factor) => (
                <View key={factor.factorName} style={styles.factor}>
                  <View style={styles.factorHead}>
                    <Text style={styles.factorName}>{factor.factorName}</Text>
                    <Num size={fontSize.sm} color={accent}>
                      ×{factor.multiplier.toFixed(2)}
                    </Num>
                  </View>
                  <Muted>{factor.rationale}</Muted>
                </View>
              ))}
            </Card>

            <Card style={styles.card}>
              <Text style={styles.sectionTitle}>Provenance</Text>
              <Muted>
                spot {formatMoney(valuation.provenance.spotPricePerTroyOunce.amount)}/ozt ·{' '}
                {valuation.provenance.source} · {valuation.provenance.method} (
                {valuation.provenance.methodVersion})
              </Muted>
              <Muted>computed {formatTimestamp(valuation.computedAtUtc)}</Muted>
            </Card>
          </>
        ) : holding ? (
          <Muted>Valuation pending…</Muted>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

function CoinImage({ label, url }: { label: string; url?: string | null }) {
  if (!url) return null;
  return (
    <View style={styles.coinWrap}>
      <Image
        source={{ uri: normalizeImageUrl(url) }}
        style={styles.coinImage}
        accessibilityLabel={`${label} reference image`}
      />
      <Text style={styles.coinLabel}>{label}</Text>
    </View>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.stat}>
      <Muted>{label}</Muted>
      <Num size={fontSize.sm}>{value}</Num>
    </View>
  );
}

function formatMoney(amount: number): string {
  return amount.toLocaleString('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' });
}

const styles = StyleSheet.create({
  content: { gap: space[3], paddingBottom: space[8] },
  back: { flexDirection: 'row', alignItems: 'center', gap: 2 },
  backLabel: { color: colors.focus, fontSize: fontSize.sm },
  error: { color: colors.negative, fontSize: fontSize.sm },
  imageRow: { flexDirection: 'row', gap: space[3] },
  coinWrap: { flex: 1, alignItems: 'center', gap: space[1] },
  coinImage: {
    width: '100%',
    aspectRatio: 1,
    borderRadius: radius.md,
    backgroundColor: colors.surfaceRaised,
  },
  coinLabel: { color: colors.textMuted, fontSize: fontSize.xs },
  card: { gap: space[2] },
  name: {
    color: colors.text,
    fontSize: fontSize.lg,
    fontWeight: fontWeight.semibold,
  },
  statRow: { flexDirection: 'row', gap: space[4], marginTop: space[1] },
  stat: { gap: 2 },
  sectionTitle: {
    color: colors.text,
    fontSize: fontSize.base,
    fontWeight: fontWeight.semibold,
  },
  factor: { gap: 2 },
  factorHead: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  factorName: { color: colors.text, fontSize: fontSize.sm, fontWeight: fontWeight.medium },
});
