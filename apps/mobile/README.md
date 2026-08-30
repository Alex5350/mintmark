# Mintmark mobile (Expo SDK 57)

The camera-first client: catalog holdings, check live spot, and - the reason
this app lives on a phone - photograph a coin obverse and reverse and get a
grounded catalog identification with top-5 candidates you confirm.

**Status, honestly:** the app is complete and typecheck-clean, passes
`expo-doctor` 21/21, and has been **run end-to-end on an iOS simulator**
(iPhone 17 Pro, iOS 26.3, Expo Go + Metro): sign-in against the live API,
collection with rollup and per-holding valuations, prices, settings
(JWT-derived account, biometric availability), and every marketing
screenshot in the repo README comes from that run. Two known caveats
remain: **camera capture and biometric unlock are not exercised on real
hardware** (the simulator has no camera/Face ID), and no EAS development
build has been cut yet - treat the first on-device run as part of
onboarding. Two contract fixes landed from that simulator run: the API
base URL defaults to IPv4 loopback (`127.0.0.1`) because the simulator
sandbox cannot open IPv6 loopback (`localhost` resolves to `::1` first),
and the hand-written wire types were realigned to the committed OpenAPI
shapes (see repo `docs/open-questions.md` #1).

## Setup

Requirements: Node 22, pnpm 11, and the Expo Go app on your phone (or an
iOS/Android toolchain for development builds).

```bash
# from the repo root
pnpm install
cd apps/mobile
pnpm expo start
```

Scan the QR code with Expo Go (Android) or the Camera app (iOS, with the
Expo Go developer tool). The app talks to the API at the `apiBaseUrl` in
`app.json` → `expo.extra` (default `http://localhost:5100` - for a real
phone use your machine's LAN IP, e.g. `http://192.168.1.20:5100`, and
restart `pnpm expo start`).

## Using the app

- **Sign in / register** - tokens live ONLY in `expo-secure-store`
  (`WHEN_UNLOCKED_THIS_DEVICE_ONLY`); the user profile cache in
  AsyncStorage is non-secret. Access tokens rotate via the refresh
  endpoint on 401s.
- **Collection** - your holdings with metal-accented rows, live melt total,
  pull-to-refresh, cursor pagination. Gray rows mean the offline queue
  hasn't synced yet.
- **Prices** - per-metal spot with STALE badges and the gold:silver ratio.
  (Range charts land with the price-history endpoint wiring.)
- **Identify** - the guided flow:
  1. Frame the **obverse** in the circle overlay (glare/focus feedback at
     capture), retake if needed.
  2. Tap flip; photograph the **reverse**.
  3. Review both, then submit. Job status polls automatically (2.5s).
  4. Confirm one of the top-5 candidates - your confirmation is training
     signal, written to the append-only audit run.
  When no vision key is configured, responses are labeled **offline
  evaluator** - deterministic, honestly labeled, never claimed as model
  inference (ADR 0009).
- **Settings** - biometric lock (opt-in; falls back to device passcode),
  offline-queue status with a manual **Flush now**, sign out.

## Offline behavior

A collector in a basement safe room has no signal. Mutations that fail on
the network land in a durable SQLite queue (`pending_mutations`) with
idempotency keys, retried with exponential backoff (30s → 15 min cap) on
app focus and a 30s timer. Permanent (4xx) failures are dropped as
unrecoverable. The queue status is always visible in Settings.

## EAS build & submit

```bash
cd apps/mobile
pnpm dlx eas-cli login         # or `eas login` if installed globally
pnpm dlx eas-cli build:configure   # creates eas.json (development build first)
pnpm dlx eas-cli build --profile development --platform ios
pnpm dlx eas-cli submit --platform ios    # App Store Connect; Android via --platform android / Play Console
```

Development builds are required (SecureStore, local-authentication, and
SQLite need native modules Expo Go no longer bundles). Documented per the
Expo EAS docs: https://docs.expo.dev/build/introduction/ and
https://docs.expo.dev/submit/introduction/.

## Testing

`pnpm tsc --noEmit` (strict) and `npx expo-doctor` are the current gates.
Jest + React Native Testing Library and a Maestro flow for the
capture→confirm path are planned, not yet present - recorded in the root
`docs/open-questions.md`.
