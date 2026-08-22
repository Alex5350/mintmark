/**
 * Durable outbound mutation queue (expo-sqlite).
 *
 * Mobile mutations that fail at the transport layer (offline, flaky cell
 * coverage) are persisted locally and replayed later — with their
 * idempotency keys, so a double-send can never create a duplicate holding.
 *
 * Flush triggers: app focus (AppState -> 'active') + a 30s interval.
 * Retry policy: per-item exponential backoff capped at 15 minutes; 4xx
 * responses are permanent failures and are dropped (surfaced as lastError).
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
  const database = await ensureDb();
  flushing = true;
  publish({ flushing: true });
  try {
    const rows = await database.getAllAsync<PendingMutation>(
      'SELECT * FROM pending_mutations ORDER BY created_at ASC',
    );
    const now = Date.now();
    for (const row of rows) {
      // Per-item backoff: an item that recently failed waits out its delay
      // (derived from attempts) before another try.
      const lastAttempt = lastAttemptAt.get(row.id);
      if (lastAttempt !== undefined && now < lastAttempt + retryDelayMs(row.attempts)) {
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
          await database.runAsync(
            'UPDATE pending_mutations SET attempts = attempts + 1 WHERE id = ?',
            [row.id],
          );
          lastAttemptAt.set(row.id, Date.now());
          break;
        }
        if (error instanceof ApiError && error.status < 500) {
          // Permanent rejection: keep the queue healthy, surface it.
          await database.runAsync('DELETE FROM pending_mutations WHERE id = ?', [row.id]);
          lastAttemptAt.delete(row.id);
          publish({ lastError: `${row.method} ${row.path}: ${error.message}` });
        } else {
          // 5xx — retryable, but with backoff.
          await database.runAsync(
            'UPDATE pending_mutations SET attempts = attempts + 1 WHERE id = ?',
            [row.id],
          );
          lastAttemptAt.set(row.id, Date.now());
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

/** In-memory last-attempt timestamps backing the per-item backoff. */
const lastAttemptAt = new Map<string, number>();

// ---------------------------------------------------------------------------
// React bindings
// ---------------------------------------------------------------------------

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
