/**
 * Durable outbound mutation queue (expo-sqlite).
 *
 * Mobile mutations that fail at the transport layer (offline, flaky cell
 * coverage) are persisted locally and replayed later — with their
 * idempotency keys, so a double-send can never create a duplicate holding.
 *
 * Flush triggers: app focus (AppState -> 'active') + a 30s interval.
 * Retry policy: per-item exponential backoff (persisted, capped 15 min);
 * 401/429 are retryable (session/rate-limit states change), 404 is a
 * permanent drop (the target is gone), other 4xx are permanent failures
 * dropped and surfaced as lastError. The queue is skipped entirely while
 * signed out and cleared on sign-out (rows are not user-scoped, so a
 * successor session must not replay them).
 *
 * Queue status is published through a tiny subscribe/getSnapshot pair —
 * consumable via useSyncExternalStore — and wrapped in <SyncProvider> for
 * the Settings readout.
 */
import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from 'react';
import { AppState } from 'react-native';
import * as SQLite from 'expo-sqlite';
import { ApiError, isNetworkError, newIdempotencyKey, queuedRequest } from './api';
import { getTokens } from './tokens';

const DB_NAME = 'mintmark.db';
const FLUSH_INTERVAL_MS = 30_000;
const BASE_RETRY_DELAY_MS = 30_000;
const MAX_RETRY_DELAY_MS = 15 * 60_000;

export interface PendingMutation {
  id: string;
  method: string;
  path: string;
  body_json: string | null;
  idempotency_key: string | null;
  created_at: string;
  attempts: number;
  last_attempt_at: number | null;
}

export type QueueMethod = 'POST' | 'PUT' | 'PATCH' | 'DELETE';

let db: SQLite.SQLiteDatabase | null = null;
let readyPromise: Promise<void> | null = null;

// ---------------------------------------------------------------------------
// Status store (Zustand-lite: subscribe + getSnapshot for useSyncExternalStore)
// ---------------------------------------------------------------------------

export interface QueueStatus {
  ready: boolean;
  pending: number;
  flushing: boolean;
  lastError: string | null;
  lastFlushedAt: number | null;
}

let status: QueueStatus = {
  ready: false,
  pending: 0,
  flushing: false,
  lastError: null,
  lastFlushedAt: null,
};

const listeners = new Set<() => void>();

function publish(patch: Partial<QueueStatus>): void {
  status = { ...status, ...patch };
  for (const listener of listeners) listener();
}

export function subscribeToQueue(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getQueueStatus(): QueueStatus {
  return status;
}

/** Mount anywhere long-lived (root layout): owns the DB + flush triggers. */
export function useQueueEngine(): QueueStatus {
  return useSyncExternalStore(subscribeToQueue, getQueueStatus, getQueueStatus);
}

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------

async function ensureDb(): Promise<SQLite.SQLiteDatabase> {
  if (!readyPromise) {
    readyPromise = (async () => {
      const database = SQLite.openDatabaseSync(DB_NAME);
      await database.execAsync(`
        CREATE TABLE IF NOT EXISTS pending_mutations (
          id TEXT PRIMARY KEY NOT NULL,
          method TEXT NOT NULL,
          path TEXT NOT NULL,
          body_json TEXT,
          idempotency_key TEXT,
          created_at TEXT NOT NULL,
          attempts INTEGER NOT NULL DEFAULT 0
        );
      `);
      // Migration: backoff timestamps persisted with the row (v1 kept them
      // in memory only, so the first flush after a restart ignored backoff).
      await database.runAsync(
        'ALTER TABLE pending_mutations ADD COLUMN last_attempt_at INTEGER',
      ).catch(() => undefined); // column already present
      db = database;
      await refreshPendingCount();
      publish({ ready: true });
    })();
  }
  await readyPromise;
  return db as SQLite.SQLiteDatabase;
}

async function refreshPendingCount(): Promise<void> {
  const database = db;
  if (!database) return;
  const row = await database.getFirstAsync<{ count: number }>(
    'SELECT COUNT(*) AS count FROM pending_mutations',
  );
  publish({ pending: row?.count ?? 0 });
}

// ---------------------------------------------------------------------------
// Enqueue / flush
// ---------------------------------------------------------------------------

export async function enqueue(
  method: QueueMethod,
  path: string,
  body?: unknown,
  idempotencyKey: string = newIdempotencyKey(method.toLowerCase()),
): Promise<void> {
  const database = await ensureDb();
  await database.runAsync(
    `INSERT INTO pending_mutations (id, method, path, body_json, idempotency_key, created_at, attempts)
     VALUES (?, ?, ?, ?, ?, ?, 0)`,
    [
      `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`,
      method,
      path,
      body === undefined ? null : JSON.stringify(body),
      idempotencyKey,
      new Date().toISOString(),
    ],
  );
  await refreshPendingCount();
}

const retryDelayMs = (attempts: number): number =>
  Math.min(BASE_RETRY_DELAY_MS * 2 ** attempts, MAX_RETRY_DELAY_MS);

let flushing = false;

export async function flush(): Promise<void> {
  if (flushing) return;
  // Set the guard synchronously, BEFORE the first await: two triggers in
  // the same tick (AppState + interval, or a manual Flush now) must not
  // both pass the check while parked on database open.
  flushing = true;
  publish({ flushing: true });
  try {
    // Signed out: the queue holds no credentials and rows are not
    // user-scoped — replaying them under a different (or no) session would
    // 401/404 every row into deletion. Sign-out clears the queue instead.
    const tokens = await getTokens();
    if (!tokens) {
      await refreshPendingCount();
      return;
    }

    const database = await ensureDb();
    const rows = await database.getAllAsync<PendingMutation>(
      'SELECT * FROM pending_mutations ORDER BY created_at ASC',
    );
    const now = Date.now();
    for (const row of rows) {
      // Per-item backoff: an item that recently failed waits out its delay
      // (derived from attempts) before another try.
      const lastAttempt = row.last_attempt_at ?? lastAttemptAt.get(row.id);
      if (lastAttempt !== undefined && lastAttempt !== null && now < lastAttempt + retryDelayMs(row.attempts)) {
        continue;
      }
      try {
        await queuedRequest(row.method as QueueMethod, row.path, {
          body: row.body_json ? (JSON.parse(row.body_json) as unknown) : undefined,
          idempotencyKey: row.idempotency_key ?? undefined,
        });
        await database.runAsync('DELETE FROM pending_mutations WHERE id = ?', [row.id]);
        lastAttemptAt.delete(row.id);
      } catch (error) {
        if (isNetworkError(error)) {
          // Still offline — bump attempts, wait out the backoff, stop here.
          await markAttempt(database, row.id, row.attempts);
          break;
        }
        if (error instanceof ApiError && (error.status === 401 || error.status === 429)) {
          // Session-expiry and rate-limit rejections are STATES, not
          // verdicts: retry later (the app refreshes tokens; the limiter
          // window rolls over) instead of destroying the mutation.
          await markAttempt(database, row.id, row.attempts);
          continue;
        }
        if (error instanceof ApiError && error.status < 500) {
          // Permanent rejection: keep the queue healthy, surface it.
          await database.runAsync('DELETE FROM pending_mutations WHERE id = ?', [row.id]);
          lastAttemptAt.delete(row.id);
          publish({ lastError: `${row.method} ${row.path}: ${error.message}` });
        } else {
          // 5xx — retryable, but with backoff.
          await markAttempt(database, row.id, row.attempts);
        }
      }
    }
    publish({ lastFlushedAt: Date.now() });
  } finally {
    flushing = false;
    publish({ flushing: false });
    await refreshPendingCount();
  }
}

async function markAttempt(
  database: SQLite.SQLiteDatabase,
  id: string,
  attempts: number,
): Promise<void> {
  const now = Date.now();
  await database.runAsync(
    'UPDATE pending_mutations SET attempts = attempts + 1, last_attempt_at = ? WHERE id = ?',
    [now, id],
  );
  lastAttemptAt.set(id, now);
}

/** Clears every queued mutation (sign-out: rows are not user-scoped). */
export async function clearQueue(): Promise<void> {
  const database = await ensureDb();
  await database.runAsync('DELETE FROM pending_mutations');
  lastAttemptAt.clear();
  publish({ lastError: null });
  await refreshPendingCount();
}

/** In-memory mirror of last_attempt_at (kept for fast reads). */
const lastAttemptAt = new Map<string, number>();

const SyncContext = createContext<QueueStatus | null>(null);

export function SyncProvider({ children }: { children: ReactNode }) {
  const queue = useQueueEngine();

  useEffect(() => {
    void ensureDb();
    const interval = setInterval(() => void flush(), FLUSH_INTERVAL_MS);
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') void flush();
    });
    return () => {
      clearInterval(interval);
      subscription.remove();
    };
  }, []);

  const value = useMemo(() => queue, [queue]);
  return <SyncContext.Provider value={value}>{children}</SyncContext.Provider>;
}

export function useSyncStatus(): QueueStatus {
  const context = useContext(SyncContext);
  if (!context) throw new Error('useSyncStatus must be used inside <SyncProvider>');
  return context;
}

/** Standalone hook (outside the provider tree) for imperative callers. */
export function useOfflineQueueStatus(): QueueStatus {
  return useSyncExternalStore(subscribeToQueue, getQueueStatus, getQueueStatus);
}
