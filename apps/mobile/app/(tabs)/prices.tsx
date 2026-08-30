/**
 * Spot quotes per metal with stale badges ("stale is never silent") and the
 * gold:silver ratio. Range chips are a non-functional placeholder until the
 * price-history endpoint ships (LTTB/downsampling server-side).
 */
import { useCallback, useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Screen } from '../../components/Screen';
import { Badge, Card, Muted, Num } from '../../components/ui';
import { api, type SpotQuote } from '../../lib/api';
import { metalLabel, type Metal } from '../../lib/enums';
import { colors, fontSize, fontWeight, metalColor, radius, space, tabular } from '../../lib/theme';

const RANGES = ['1D', '1W', '1M', '3M', '1Y', '5Y', 'MAX'] as const;

export default function PricesScreen() {
  const [quotes, setQuotes] = useState<SpotQuote[] | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setQuotes(await api.prices.current());
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

  const gold = quotes?.find((quote) => quote.metal === 0);
  const silver = quotes?.find((quote) => quote.metal === 1);
  const ratio =
    gold && silver && silver.price > 0 ? gold.price / silver.price : null;
  const latest = quotes?.reduce<string>((best, quote) =>
    quote.sourceTimestampUtc > best ? quote.sourceTimestampUtc : best, '');

  return (
    <Screen title="Prices" subtitle={latest ? formatTimestamp(latest) : undefined}>
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

        {(quotes ?? []).map((quote) => (
          <QuoteRow key={quote.metal} quote={quote} />
        ))}
        {!quotes && !error ? <Muted>Loading spot prices…</Muted> : null}

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
  const label = metalLabel(quote.metal);
  const accent = metalColor(label);
  return (
    <Card style={styles.quote}>
      <View style={styles.quoteRow}>
        <View style={styles.quoteId}>
          <View style={[styles.dot, { backgroundColor: accent }]} />
          <Text style={styles.metal}>{label}</Text>
        </View>
        {quote.isStale ? <Badge label="stale" /> : null}
      </View>
      <View style={styles.quoteRow}>
        <Num size={fontSize.xl} color={accent}>
          {formatMoney(quote.price)}
          <Text style={styles.unit}> / ozt</Text>
        </Num>
        {quote.provider ? <Muted>{quote.provider}</Muted> : null}
      </View>
      <Muted>
        bid {formatMoney(quote.bid)} · ask {formatMoney(quote.ask)}
      </Muted>
      <Muted>as of {formatTimestamp(quote.sourceTimestampUtc)}</Muted>
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
