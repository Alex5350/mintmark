/**
 * Holdings list: portfolio rollup header (value, unrealized %, basis),
 * metal-accented rows (display name, quantity, live collectible value from
 * the per-holding valuation), pull-to-refresh, cursor pagination, empty state.
 */
import { Ionicons } from '@expo/vector-icons';
import { Link } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
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
  // Set synchronously when a page fetch begins: onEndReached can fire many
  // times per scroll gesture and double-append the same cursor otherwise.
  const loadingMoreRef = useRef(false);
  // Enrichment epoch: a slower, older enrichment pass must not overwrite a
  // newer page list (which would visibly drop freshly appended rows).
  const enrichEpochRef = useRef(0);

  // The list endpoint does not return current values (valuation output is
  // per-holding), so values are enriched client-side from the valuation
  // endpoint — same call the detail screen makes, fetched only for NEW ids
  // and merged by id. Failures leave the row without a value rather than
  // failing the screen.
  const enrichWithValues = useCallback(async (epoch: number, items: HoldingListItem[]) => {
    const settled = await Promise.all(
      items.map(async (item) => {
        try {
          const valuation = await api.holdings.valuation(item.id);
          return { id: item.id, value: valuation.collectible.amount } as const;
        } catch {
          return null;
        }
      }),
    );
    if (enrichEpochRef.current !== epoch) return; // a newer pass owns the rows
    const values = new Map(
      settled.filter((r): r is { id: number; value: number } => r !== null).map((r) => [r.id, r.value]),
    );
    setRows((previous) =>
      previous.map((row) =>
        values.has(row.id) ? { ...row, value: values.get(row.id) } : row,
      ),
    );
  }, []);

  const load = useCallback(async (cursor?: string | null) => {
    if (cursor) {
      if (loadingMoreRef.current) return;
      loadingMoreRef.current = true;
    }
    if (cursor === undefined) setLoading(true);
    try {
      const [next, rollupResult] = await Promise.all([
        api.holdings.list(cursor ?? undefined),
        cursor ? Promise.resolve(null) : api.portfolio.rollup().catch(() => null),
      ]);
      setError(null);
      if (rollupResult) setRollup(rollupResult);
      const epoch = ++enrichEpochRef.current;
      setPage((previous) =>
        cursor
          ? { items: [...previous.items, ...next.items], nextCursor: next.nextCursor }
          : next,
      );
      setRows((previous) => {
        const previousById = new Map(previous.map((row) => [row.id, row]));
        // Reset on refresh (no cursor), append-and-preserve on pagination:
        // already-enriched rows keep their values instead of refetching.
        const base = cursor
          ? [...previous, ...next.items.filter((item) => !previousById.has(item.id))]
          : next.items;
        void enrichWithValues(epoch, next.items);
        return base;
      });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load holdings.');
    } finally {
      loadingMoreRef.current = false;
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
    <Screen
      title="Collection"
      subtitle={
        rollup
          ? `${rollup.holdingCount} holdings tracked`
          : `${page.items.length} holdings tracked`
      }
    >
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
        {rollup.unrealizedPct != null ? (
          <Num
            size={fontSize.sm}
            color={gain >= 0 ? colors.positive : colors.negative}
          >
            {gain >= 0 ? '+' : ''}
            {gain.toFixed(2)}%
          </Num>
        ) : null}
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
