/**
 * Spot quotes per metal with stale badges ("stale is never silent") and the
 * gold:silver ratio. Range chips are a non-functional placeholder until the
 * price-history endpoint ships (LTTB/downsampling server-side).
 */
import { useCallback, useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Screen } from '../../components/Screen';
import { Badge, Card, Muted, Num } from '../../components/ui';
import { api, type PricesCurrent, type SpotQuote } from '../../lib/api';
import { colors, fontSize, fontWeight, metalColor, radius, space, tabular } from '../../lib/theme';

const RANGES = ['1D', '1W', '1M', '3M', '1Y', '5Y', 'MAX'] as const;

export default function PricesScreen() {
  const [prices, setPrices] = useState<PricesCurrent | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setPrices(await api.prices.current());
      setError(null);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load prices.');
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const gold = prices?.quotes.find((quote) => quote.metal === 'Gold');
  const silver = prices?.quotes.find((quote) => quote.metal === 'Silver');
  const ratio =
    gold && silver && silver.pricePerOzt > 0 ? gold.pricePerOzt / silver.pricePerOzt : null;

  return (
    <Screen title="Prices" subtitle={formatTimestamp(prices?.asOf)}>
      <ScrollView
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={() => {
              setRefreshing(true);
              void load();
            }}
            tintColor={colors.textMuted}
          />
        }
        contentContainerStyle={styles.content}
      >
        {error ? <Text style={styles.error}>{error}</Text> : null}

        {ratio !== null ? (
          <Card style={styles.ratioCard}>
            <Muted>Gold : Silver ratio</Muted>
            <Num size={fontSize.xl} color={colors.goldSoft}>
              {ratio.toFixed(1)} : 1
            </Num>
          </Card>
        ) : null}

        {(prices?.quotes ?? []).map((quote) => (
          <QuoteRow key={quote.metal} quote={quote} />
        ))}
        {!prices && !error ? <Muted>Loading spot prices…</Muted> : null}

        <View style={styles.chips}>
          {RANGES.map((range) => (
            <Pressable key={range} style={styles.chip} disabled>
              <Text style={styles.chipLabel}>{range}</Text>
            </Pressable>
          ))}
        </View>
        <Muted>charts ship with price history endpoint</Muted>
      </ScrollView>
    </Screen>
  );
}

function QuoteRow({ quote }: { quote: SpotQuote }) {
  const accent = metalColor(quote.metal);
  const change = quote.changePercent24h ?? null;
  return (
    <Card style={styles.quote}>
      <View style={styles.quoteRow}>
        <View style={styles.quoteId}>
          <View style={[styles.dot, { backgroundColor: accent }]} />
          <Text style={styles.metal}>{quote.metal}</Text>
        </View>
        {quote.stale ? <Badge label="stale" /> : null}
      </View>
      <View style={styles.quoteRow}>
        <Num size={fontSize.xl} color={accent}>
          {formatMoney(quote.pricePerOzt)}
          <Text style={styles.unit}> / ozt</Text>
        </Num>
        {change !== null ? (
          <Num color={change >= 0 ? colors.positive : colors.negative} size={fontSize.sm}>
            {change >= 0 ? '+' : ''}
            {change.toFixed(2)}%
          </Num>
        ) : null}
      </View>
      <Muted>as of {formatTimestamp(quote.asOf)}</Muted>
    </Card>
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

function formatTimestamp(iso?: string): string {
  if (!iso) return '';
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleString('en-US', { dateStyle: 'medium', timeStyle: 'short' });
}

const styles = StyleSheet.create({
  content: { gap: space[3], paddingBottom: space[8] },
  error: { color: colors.negative, fontSize: fontSize.sm },
  ratioCard: { gap: space[1] },
  quote: { gap: space[2] },
  quoteRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  quoteId: { flexDirection: 'row', alignItems: 'center', gap: space[2] },
  dot: { width: 10, height: 10, borderRadius: 5 },
  metal: { color: colors.text, fontSize: fontSize.lg, fontWeight: fontWeight.semibold },
  unit: { color: colors.textMuted, fontSize: fontSize.sm },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: space[2], marginTop: space[2] },
  chip: {
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.full,
    paddingVertical: space[1],
    paddingHorizontal: space[3],
    opacity: 0.55,
  },
  chipLabel: { ...{ color: colors.textMuted, fontSize: fontSize.sm }, ...tabular },
});
