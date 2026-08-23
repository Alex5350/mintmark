"use client";

/**
 * Auth token store. Zustand, in memory only — Bearer tokens live in the
 * Authorization header (ADR 0008), never in cookies or durable storage, so a
 * reload means signing in again. The API client reads/writes tokens
 * imperatively via `getState()`.
 */
import { create } from "zustand";
import type { AuthTokens } from "@/lib/api-types";

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  setTokens: (tokens: AuthTokens) => void;
  clear: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  refreshToken: null,
  setTokens: ({ accessToken, refreshToken }) => set({ accessToken, refreshToken }),
  clear: () => set({ accessToken: null, refreshToken: null }),
}));
