"use client";

/**
 * Auth token store. Zustand + localStorage persistence so a page reload keeps
 * the session; the API client reads tokens imperatively via `getState()`.
 */
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { TokenPair } from "@/lib/api-types";

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  setTokens: (tokens: TokenPair) => void;
  clear: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      setTokens: ({ accessToken, refreshToken }) => set({ accessToken, refreshToken }),
      clear: () => set({ accessToken: null, refreshToken: null }),
    }),
    { name: "mintmark.auth" },
  ),
);
