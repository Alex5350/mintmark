/**
 * Holdings list: portfolio rollup header (value, unrealized %, basis),
 * metal-accented rows (display name, quantity, live collectible value from
 * the per-holding valuation), pull-to-refresh, cursor pagination, empty state.
 */
import { Ionicons } from '@expo/vector-icons';
import { Link } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import {
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Screen } from '../../../components/Screen';
import { Card, Muted, Num } from '../../../components/ui';
import {
  api,
  type HoldingListItem,
  type HoldingsPage,
  type PortfolioRollup,
} from '../../../lib/api';
import { itemFormLabel, knownMetal, metalLabel } from '../../../lib/enums';
import { colors, fontSize, fontWeight, metalColor, radius, space } from '../../../lib/theme';

type Row = HoldingListItem & { value?: number };

export default function CollectionScreen() {
  const [page, setPage] = useState<HoldingsPage>({ items: [], nextCursor: null });
  const [rows, setRows] = useState<Row[]>([]);
  const [rollup, setRollup] = useState<PortfolioRollup | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The list endpoint does not return current values (valuation output is
  // per-holding), so values are enriched client-side from the valuation
  // endpoint — same call the detail screen makes. Failures leave the row
  // without a value rather than failing the screen.
  const enrichWithValues = useCallback(async (items: HoldingListItem[]) => {
    const settled = await Promise.all(
      items.map(async (item) => {
        try {
          const valuation = await api.holdings.valuation(item.id);
          return { ...item, value: valuation.collectible.amount };
        } catch {
          return item;
        }
      }),
    );
    setRows(settled);
  }, []);

  const load = useCallback(async (cursor?: string | null) => {
    if (cursor === undefined) setLoading(true);
    try {
      const [next, rollupResult] = await Promise.all([
        api.holdings.list(cursor ?? undefined),
        cursor ? Promise.resolve(null) : api.portfolio.rollup().catch(() => null),
      ]);
      setError(null);
      if (rollupResult) setRollup(rollupResult);
      setPage((previous) =>
        cursor
          ? { items: [...previous.items, ...next.items], nextCursor: next.nextCursor }
          : next,
      );
      setRows((previous) => {
        const base = cursor ? [...previous, ...next.items] : next.items;
        void enrichWithValues(base);
        return base;
      });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load holdings.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [enrichWithValues]);

  useEffect(() => {
    void load();
  }, [load]);

  const onRefresh = useCallback(() => {
    setRefreshing(true);
    void load(null);
  }, [load]);

  return (
    <Screen title="Collection" subtitle={`${page.items.length} holdings tracked`}>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={rows}
        keyExtractor={(item) => String(item.id)}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={onRefresh}
            tintColor={colors.textMuted}
          />
        }
        onEndReachedThreshold={0.5}
        onEndReached={() => {
          if (page.nextCursor && !loading) void load(page.nextCursor);
        }}
        ListHeaderComponent={
          rollup && rollup.currentValue ? <RollupCard rollup={rollup} /> : null
        }
        ListEmptyComponent={
          !loading && !error ? (
            <View style={styles.empty}>
              <Ionicons name="cube-outline" size={44} color={colors.textMuted} />
              <Text style={styles.emptyTitle}>No coins yet</Text>
              <Muted>Identify your first coin to start the collection.</Muted>
              <Link href="/(tabs)/identify" asChild>
                <Pressable style={styles.emptyCta}>
                  <Text style={styles.emptyCtaLabel}>Identify a coin</Text>
                </Pressable>
              </Link>
            </View>
          ) : null
        }
        ItemSeparatorComponent={() => <View style={{ height: space[2] }} />}
        renderItem={({ item }) => <HoldingRow holding={item} />}
      />
    </Screen>
  );
}

function RollupCard({ rollup }: { rollup: PortfolioRollup }) {
  const gain = rollup.unrealizedPct ?? 0;
  return (
    <Card style={styles.totalCard}>
      <Muted>Portfolio value</Muted>
      <Num size={fontSize['2xl']} color={colors.gold}>
        {formatMoney(rollup.currentValue?.amount ?? 0)}
      </Num>
      <View style={styles.totalMeta}>
        <Num
          size={fontSize.sm}
          color={gain >= 0 ? colors.positive : colors.negative}
        >
          {gain >= 0 ? '+' : ''}
          {gain.toFixed(2)}%
        </Num>
        {rollup.costBasis ? (
          <Muted>
            basis {formatMoney(rollup.costBasis.amount)} · {rollup.holdingCount} holdings
          </Muted>
        ) : null}
      </View>
    </Card>
  );
}

function HoldingRow({ holding }: { holding: Row }) {
  const metal = knownMetal(holding.metal);
  const accent = metal ? metalColor(metal) : colors.textMuted;
  return (
    <Link href={`/collection/${holding.id}`} asChild>
      <Pressable style={styles.row}>
        <View style={[styles.rowAccent, { backgroundColor: accent }]} />
        <View style={styles.rowMain}>
          <View style={styles.rowHead}>
            <Text style={styles.series} numberOfLines={2}>
              {holding.displayName}
            </Text>
          </View>
          <Text style={styles.meta}>
            {[
              metalLabel(holding.metal),
              itemFormLabel(holding.form),
              `${holding.effectiveQuantity} pcs`,
            ].join(' · ')}
          </Text>
        </View>
        <View style={styles.rowNumbers}>
          {holding.value !== undefined ? (
            <Num color={accent}>{formatMoney(holding.value)}</Num>
          ) : holding.effectivePurchasePricePerUnit ? (
            <Num color={colors.textMuted}>
              {formatMoney(holding.effectivePurchasePricePerUnit.amount)}/pc
            </Num>
          ) : null}
          <Ionicons name="chevron-forward" size={16} color={colors.textMuted} />
        </View>
      </Pressable>
    </Link>
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

const styles = StyleSheet.create({
  error: { color: colors.negative, fontSize: fontSize.sm },
  totalCard: { marginBottom: space[2], gap: space[1] },
  totalMeta: { flexDirection: 'row', alignItems: 'center', gap: space[3] },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: StyleSheet.hairlineWidth,
    borderRadius: radius.md,
    padding: space[3],
    gap: space[3],
    overflow: 'hidden',
  },
  rowAccent: { width: 3, alignSelf: 'stretch', borderRadius: 2 },
  rowMain: { flex: 1, gap: 2 },
  rowHead: { flexDirection: 'row', alignItems: 'center', gap: space[2] },
  series: {
    color: colors.text,
    fontSize: fontSize.base,
    fontWeight: fontWeight.semibold,
    flexShrink: 1,
  },
  meta: { color: colors.textMuted, fontSize: fontSize.sm },
  rowNumbers: { alignItems: 'flex-end', gap: 2 },
  empty: { alignItems: 'center', gap: space[2], paddingVertical: space[12] },
  emptyTitle: {
    color: colors.text,
    fontSize: fontSize.lg,
    fontWeight: fontWeight.semibold,
  },
  emptyCta: {
    marginTop: space[2],
    backgroundColor: colors.gold,
    borderRadius: radius.md,
    paddingVertical: space[3],
    paddingHorizontal: space[6],
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
  },
  emptyCtaLabel: { color: colors.base, fontWeight: fontWeight.semibold },
});
