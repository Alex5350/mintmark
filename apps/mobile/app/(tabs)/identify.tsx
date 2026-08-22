/**
 * Guided coin capture: obverse -> "flip the coin" -> reverse -> review ->
 * submit (multipart) -> poll job status -> candidates with Confirm.
 *
 * The circular alignment overlay marks where the coin should sit for each
 * shot; expo-image-picker supplies camera + library capture.
 */
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useCallback, useEffect, useState, type ReactNode } from 'react';
import {
  ActivityIndicator,
  Image,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Screen } from '../../components/Screen';
import { Badge, Button, Card, Muted, Num } from '../../components/ui';
import { CircleOverlay } from '../../components/CircleOverlay';
import {
  api,
  isNetworkError,
  newIdempotencyKey,
  type IdentificationCandidate,
  type IdentificationJob,
  type ImagePart,
} from '../../lib/api';
import { enqueue } from '../../lib/offline-queue';
import { colors, fontSize, fontWeight, metalColor, radius, space } from '../../lib/theme';

type Side = 'obverse' | 'reverse';

type Step =
  | { kind: 'capture'; side: Side }
  | { kind: 'review' }
  | { kind: 'submitting' }
  | { kind: 'polling'; jobId: string; ticks: number }
  | { kind: 'candidates'; job: IdentificationJob }
  | { kind: 'confirmed'; queuedOffline: boolean }
  | { kind: 'failed'; message: string };

const POLL_INTERVAL_MS = 2_500;
const MAX_POLLS = 40;

export default function IdentifyScreen() {
  const [step, setStep] = useState<Step>({ kind: 'capture', side: 'obverse' });
  const [obverse, setObverse] = useState<ImagePicker.ImagePickerAsset | null>(null);
  const [reverse, setReverse] = useState<ImagePicker.ImagePickerAsset | null>(null);
  const [error, setError] = useState<string | null>(null);

  const pickFor = useCallback(async (side: Side, source: 'camera' | 'library') => {
    setError(null);
    if (source === 'camera') {
      const current = await ImagePicker.getCameraPermissionsAsync();
      if (!current.granted) {
        const requested = await ImagePicker.requestCameraPermissionsAsync();
        if (!requested.granted) {
          setError('Camera permission is needed to photograph coins.');
          return;
        }
      }
    }
    const options: ImagePicker.ImagePickerOptions = {
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.8,
      exif: false,
    };
    const result =
      source === 'camera'
        ? await ImagePicker.launchCameraAsync(options)
        : await ImagePicker.launchImageLibraryAsync(options);
    if (result.canceled) return;
    const asset = result.assets?.[0];
    if (!asset) return;
    if (side === 'obverse') {
      setObverse(asset);
      setStep({ kind: 'capture', side: 'reverse' }); // "flip the coin"
    } else {
      setReverse(asset);
      setStep({ kind: 'review' });
    }
  }, []);

  const submit = useCallback(async () => {
    if (!obverse || !reverse) return;
    setStep({ kind: 'submitting' });
    setError(null);
    try {
      const job = await api.identification.submit({
        obverse: toImagePart(obverse, 'obverse'),
        reverse: toImagePart(reverse, 'reverse'),
      });
      setStep({ kind: 'polling', jobId: job.id, ticks: 0 });
    } catch (cause) {
      setStep({
        kind: 'failed',
        message: cause instanceof Error ? cause.message : 'Submission failed.',
      });
    }
  }, [obverse, reverse]);

  // Async by design: clients poll a job status; no request blocks on the
  // model call.
  useEffect(() => {
    if (step.kind !== 'polling') return;
    const { jobId } = step;
    let active = true;
    const timer = setInterval(async () => {
      if (!active) return;
      try {
        const job = await api.identification.get(jobId);
        if (!active) return;
        if (job.status === 'completed') {
          clearInterval(timer);
          if (job.candidates?.length) {
            setStep({ kind: 'candidates', job });
          } else {
            setStep({
              kind: 'failed',
              message: 'No matches found. Try sharper, well-lit photos.',
            });
          }
        } else if (job.status === 'failed') {
          clearInterval(timer);
          setStep({ kind: 'failed', message: job.error ?? 'Identification failed.' });
        } else if ((step.ticks ?? 0) + 1 >= MAX_POLLS) {
          clearInterval(timer);
          setStep({ kind: 'failed', message: 'Timed out waiting for results.' });
        } else {
          setStep((previous) =>
            previous.kind === 'polling'
              ? { ...previous, ticks: previous.ticks + 1 }
              : previous,
          );
        }
      } catch (cause) {
        if (!active) return;
        if (isNetworkError(cause)) return; // transient — keep polling
        clearInterval(timer);
        setStep({
          kind: 'failed',
          message: cause instanceof Error ? cause.message : 'Polling failed.',
        });
      }
    }, POLL_INTERVAL_MS);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [step]);

  const confirm = useCallback(async (jobId: string, candidate: IdentificationCandidate) => {
    setError(null);
    const idempotencyKey = newIdempotencyKey('confirm');
    try {
      await api.identification.confirm(jobId, candidate.id, idempotencyKey);
      setStep({ kind: 'confirmed', queuedOffline: false });
    } catch (cause) {
      if (isNetworkError(cause)) {
        // Offline: the durable queue replays the confirm (with its
        // idempotency key) once connectivity returns.
        await enqueue(
          'POST',
          `/api/v1/identification/${jobId}/confirm`,
          { candidateId: candidate.id },
          idempotencyKey,
        );
        setStep({ kind: 'confirmed', queuedOffline: true });
        return;
      }
      setStep({
        kind: 'failed',
        message: cause instanceof Error ? cause.message : 'Confirm failed.',
      });
    }
  }, []);

  const reset = useCallback(() => {
    setObverse(null);
    setReverse(null);
    setError(null);
    setStep({ kind: 'capture', side: 'obverse' });
  }, []);

  return (
    <Screen title="Identify" subtitle="Two shots: front, then back">
      <ScrollView contentContainerStyle={styles.content}>
        {error ? <Text style={styles.error}>{error}</Text> : null}

        {step.kind === 'capture' ? (
          <CaptureStep
            side={step.side}
            obverse={obverse}
            reverse={reverse}
            onCapture={(side) => void pickFor(side, 'camera')}
            onPick={(side) => void pickFor(side, 'library')}
          />
        ) : null}

        {step.kind === 'review' ? (
          <ReviewStep
            obverse={obverse}
            reverse={reverse}
            onRetake={(side) => setStep({ kind: 'capture', side })}
            onSubmit={() => void submit()}
          />
        ) : null}

        {step.kind === 'submitting' ? (
          <Centered>
            <ActivityIndicator size="large" color={colors.gold} />
            <Text style={styles.statusText}>Uploading both sides…</Text>
          </Centered>
        ) : null}

        {step.kind === 'polling' ? (
          <Centered>
            <ActivityIndicator size="large" color={colors.gold} />
            <Text style={styles.statusText}>Analyzing your coin…</Text>
            <Muted>
              job {step.jobId} · check {step.ticks + 1}/{MAX_POLLS}
            </Muted>
          </Centered>
        ) : null}

        {step.kind === 'candidates' ? (
          <CandidatesStep
            job={step.job}
            onConfirm={(candidate) => void confirm(step.job.id, candidate)}
          />
        ) : null}

        {step.kind === 'confirmed' ? (
          <Centered>
            <Ionicons name="checkmark-circle" size={48} color={colors.positive} />
            <Text style={styles.statusText}>Confirmed</Text>
            {step.queuedOffline ? (
              <Badge label="confirm queued — will sync when online" color={colors.warning} />
            ) : (
              <Muted>Added to your collection.</Muted>
            )}
            <Button label="Identify another coin" onPress={reset} variant="ghost" />
          </Centered>
        ) : null}

        {step.kind === 'failed' ? (
          <Centered>
            <Ionicons name="warning" size={44} color={colors.warning} />
            <Text style={styles.statusText}>{step.message}</Text>
            <Button label="Start over" onPress={reset} />
          </Centered>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

// --- steps -----------------------------------------------------------------

function CaptureStep({
  side,
  obverse,
  reverse,
  onCapture,
  onPick,
}: {
  side: Side;
  obverse: ImagePicker.ImagePickerAsset | null;
  reverse: ImagePicker.ImagePickerAsset | null;
  onCapture: (side: Side) => void;
  onPick: (side: Side) => void;
}) {
  const preview = side === 'obverse' ? obverse : reverse;
  const other = side === 'obverse' ? reverse : obverse;
  return (
    <View style={styles.stepGap}>
      {side === 'reverse' ? (
        <Card style={styles.flipNote}>
          <Ionicons name="sync" size={18} color={colors.gold} />
          <Text style={styles.flipText}>Got the front — now flip the coin over.</Text>
        </Card>
      ) : null}
      <View style={styles.viewport}>
        {preview ? (
          <Image source={{ uri: preview.uri }} style={styles.viewportImage} />
        ) : null}
        <CircleOverlay />
        <View style={styles.sideTag}>
          <Text style={styles.sideTagLabel}>{side}</Text>
        </View>
      </View>
      <Text style={styles.instruction}>
        {side === 'obverse'
          ? 'Center the HEADS side in the circle and fill it edge to edge.'
          : 'Center the TAILS side in the circle and fill it edge to edge.'}
      </Text>
      {other ? (
        <Muted>Other side captured — you can retake it from the review screen.</Muted>
      ) : null}
      <Button label={`Capture ${side}`} onPress={() => onCapture(side)} />
      <Button label="Choose from library" variant="ghost" onPress={() => onPick(side)} />
    </View>
  );
}

function ReviewStep({
  obverse,
  reverse,
  onRetake,
  onSubmit,
}: {
  obverse: ImagePicker.ImagePickerAsset | null;
  reverse: ImagePicker.ImagePickerAsset | null;
  onRetake: (side: Side) => void;
  onSubmit: () => void;
}) {
  return (
    <View style={styles.stepGap}>
      <View style={styles.reviewRow}>
        <ReviewThumb side="obverse" asset={obverse} onRetake={onRetake} />
        <ReviewThumb side="reverse" asset={reverse} onRetake={onRetake} />
      </View>
      <Muted>Both sides are required before the vision pipeline runs.</Muted>
      <Button label="Submit for identification" onPress={onSubmit} />
      <Button label="Start over" variant="ghost" onPress={() => onRetake('obverse')} />
    </View>
  );
}

function ReviewThumb({
  side,
  asset,
  onRetake,
}: {
  side: Side;
  asset: ImagePicker.ImagePickerAsset | null;
  onRetake: (side: Side) => void;
}) {
  return (
    <View style={styles.thumbWrap}>
      {asset ? (
        <Image source={{ uri: asset.uri }} style={styles.thumb} />
      ) : (
        <View style={[styles.thumb, styles.thumbEmpty]} />
      )}
      <Text style={styles.thumbLabel}>{side}</Text>
      <Pressable onPress={() => onRetake(side)} hitSlop={8}>
        <Text style={styles.retake}>retake</Text>
      </Pressable>
    </View>
  );
}

function CandidatesStep({
  job,
  onConfirm,
}: {
  job: IdentificationJob;
  onConfirm: (candidate: IdentificationCandidate) => void;
}) {
  const candidates = job.candidates ?? [];
  return (
    <View style={styles.stepGap}>
      <Muted>Top {candidates.length} candidates — confirm the match.</Muted>
      {candidates.map((candidate, index) => {
        const accent = metalColor(candidate.metal);
        return (
          <Card key={candidate.id} style={styles.candidate}>
            <View style={styles.candidateRow}>
              <Text style={[styles.rank, { color: accent }]}>#{index + 1}</Text>
              <View style={styles.candidateMain}>
                <Text style={styles.candidateSeries} numberOfLines={2}>
                  {candidate.series}
                </Text>
                <Text style={styles.candidateMeta}>
                  {[candidate.metal, candidate.yearRange, candidate.catalogNo]
                    .filter(Boolean)
                    .join(' · ')}
                </Text>
              </View>
              <Num color={accent}>{Math.round(candidate.confidence * 100)}%</Num>
            </View>
            <Button label="Confirm" onPress={() => onConfirm(candidate)} />
          </Card>
        );
      })}
    </View>
  );
}

function Centered({ children }: { children: ReactNode }) {
  return <View style={styles.centered}>{children}</View>;
}

function toImagePart(
  asset: ImagePicker.ImagePickerAsset,
  side: Side,
): ImagePart {
  return {
    uri: asset.uri,
    name: asset.fileName ?? `${side}.jpg`,
    type: asset.mimeType ?? 'image/jpeg',
  };
}

const styles = StyleSheet.create({
  content: { gap: space[3], paddingBottom: space[8] },
  error: { color: colors.negative, fontSize: fontSize.sm },
  stepGap: { gap: space[3] },
  flipNote: { flexDirection: 'row', alignItems: 'center', gap: space[2] },
  flipText: { color: colors.text, fontSize: fontSize.sm, flex: 1 },
  viewport: {
    aspectRatio: 1,
    borderRadius: radius.lg,
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: StyleSheet.hairlineWidth,
    overflow: 'hidden',
  },
  viewportImage: { width: '100%', height: '100%' },
  sideTag: {
    position: 'absolute',
    top: space[2],
    right: space[2],
    backgroundColor: 'rgba(14, 17, 22, 0.8)',
    borderRadius: radius.full,
    paddingVertical: 2,
    paddingHorizontal: space[2],
  },
  sideTagLabel: {
    color: colors.goldSoft,
    fontSize: fontSize.xs,
    fontWeight: fontWeight.semibold,
    textTransform: 'capitalize',
  },
  instruction: { color: colors.textMuted, fontSize: fontSize.sm, textAlign: 'center' },
  reviewRow: { flexDirection: 'row', gap: space[3] },
  thumbWrap: { flex: 1, alignItems: 'center', gap: space[1] },
  thumb: {
    width: '100%',
    aspectRatio: 1,
    borderRadius: radius.md,
    backgroundColor: colors.surfaceRaised,
  },
  thumbEmpty: { borderWidth: 1, borderColor: colors.border },
  thumbLabel: {
    color: colors.textMuted,
    fontSize: fontSize.xs,
    textTransform: 'capitalize',
  },
  retake: { color: colors.focus, fontSize: fontSize.sm },
  centered: { alignItems: 'center', gap: space[3], paddingVertical: space[12] },
  statusText: {
    color: colors.text,
    fontSize: fontSize.lg,
    fontWeight: fontWeight.semibold,
    textAlign: 'center',
  },
  candidate: { gap: space[3] },
  candidateRow: { flexDirection: 'row', alignItems: 'center', gap: space[3] },
  rank: { fontSize: fontSize.lg, fontWeight: fontWeight.bold },
  candidateMain: { flex: 1, gap: 2 },
  candidateSeries: { color: colors.text, fontSize: fontSize.base, fontWeight: fontWeight.semibold },
  candidateMeta: { color: colors.textMuted, fontSize: fontSize.sm },
});
