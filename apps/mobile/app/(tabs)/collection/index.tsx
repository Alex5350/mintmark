/**
 * Holdings list: metal-accented rows (series, quantity, melt value with
 * tabular figures), pull-to-refresh, cursor pagination, empty state.
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
import { Badge, Card, Muted, Num } from '../../../components/ui';
import { api, type HoldingsPage, type Holding } from '../../../lib/api';
import { colors, fontSize, fontWeight, metalColor, radius, space } from '../../../lib/theme';

export default function CollectionScreen() {
  const [page, setPage] = useState<HoldingsPage>({ items: [], nextCursor: null });
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (cursor?: string | null) => {
    if (cursor === undefined) setLoading(true);
    try {
      const next = await api.holdings.list(cursor ?? undefined);
      setError(null);
      setPage((previous) =>
        cursor
          ? { items: [...previous.items, ...next.items], nextCursor: next.nextCursor }
          : next,
      );
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load holdings.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const onRefresh = useCallback(() => {
    setRefreshing(true);
    void load(null);
  }, [load]);

  const totalMelt = page.items.reduce(
    (sum, holding) => sum + (holding.meltValue?.amount ?? 0) * holding.quantity,
    0,
  );

  return (
    <Screen title="Collection" subtitle={`${page.items.length} series tracked`}>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={page.items}
        keyExtractor={(item) => item.id}
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
          page.items.length > 0 ? (
            <Card style={styles.totalCard}>
              <Muted>Portfolio melt value</Muted>
              <Num size={fontSize['2xl']} color={colors.gold}>
                ${formatMoney(totalMelt)}
              </Num>
            </Card>
          ) : null
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

function HoldingRow({ holding }: { holding: Holding }) {
  const accent = metalColor(holding.metal);
  return (
    <View style={styles.row}>
      <View style={[styles.rowAccent, { backgroundColor: accent }]} />
      <View style={styles.rowMain}>
        <View style={styles.rowHead}>
          <Text style={styles.series} numberOfLines={1}>
            {holding.series}
          </Text>
          {holding.meltValue?.stale ? <Badge label="stale" /> : null}
        </View>
        <Text style={styles.meta}>
          {[holding.year, holding.mintMark, holding.metal].filter(Boolean).join(' · ')}
        </Text>
      </View>
      <View style={styles.rowNumbers}>
        <Num>{holding.quantity} pcs</Num>
        <Num color={accent}>
          ${formatMoney((holding.meltValue?.amount ?? 0) * holding.quantity)}
        </Num>
      </View>
    </View>
  );
}

function formatMoney(amount: number): string {
  return amount.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

const styles = StyleSheet.create({
  error: { color: colors.negative, fontSize: fontSize.sm },
  totalCard: { marginBottom: space[2], gap: space[1] },
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
