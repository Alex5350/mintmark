"use client";

/**
 * Identification upload/capture shell. Two-shot obverse+reverse capture,
 * submit → job id (202), polling while queued, per-field confidence chips,
 * ranked candidates (names resolved from the catalog) with confirm buttons,
 * and the provider label the run actually used. No request ever blocks on a
 * model call.
 */
import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@/lib/api";
import type { IdentificationStatusResponse } from "@/lib/api-types";
import { identificationStatusLabel, identificationStatusPolling } from "@/lib/enums";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/cn";

interface Shot {
  file: File;
  previewUrl: string;
}

function useShot() {
  const [shot, setShot] = useState<Shot | null>(null);
  const previousUrl = useRef<string | null>(null);

  useEffect(() => {
    return () => {
      if (previousUrl.current) URL.revokeObjectURL(previousUrl.current);
    };
  }, []);

  function set(file: File | undefined) {
    if (!file) return;
    if (previousUrl.current) URL.revokeObjectURL(previousUrl.current);
    const previewUrl = URL.createObjectURL(file);
    previousUrl.current = previewUrl;
    setShot({ file, previewUrl });
  }

  return [shot, set] as const;
}

function ShotSlot({
  side,
  shot,
  onSelect,
}: {
  side: "obverse" | "reverse";
  shot: Shot | null;
  onSelect: (file: File | undefined) => void;
}) {
  const inputId = `identify-${side}`;
  return (
    <div className="flex flex-col items-center gap-2">
      <label
        htmlFor={inputId}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          onSelect(e.dataTransfer.files?.[0]);
        }}
        className={cn(
          "relative flex aspect-square w-36 cursor-pointer items-center justify-center overflow-hidden rounded-full border border-dashed border-border bg-surface-raised",
          "transition-colors hover:border-focus focus-within:outline-none focus-within:ring-2 focus-within:ring-focus",
        )}
      >
        {shot ? (
          <Image
            src={shot.previewUrl}
            alt={`${side} capture preview`}
            fill
            unoptimized
            sizes="144px"
            className="object-cover"
          />
        ) : (
          <span className="px-4 text-center text-xs text-ink-muted">
            <span aria-hidden="true" className="font-heading block text-lg">
              {side === "obverse" ? "O" : "R"}
            </span>
            Add {side}
          </span>
        )}
        <input
          id={inputId}
          type="file"
          accept="image/*"
          className="sr-only"
          onChange={(e) => onSelect(e.target.files?.[0])}
        />
      </label>
      <span className="text-xs text-ink-muted capitalize">{side}</span>
    </div>
  );
}

function confidenceTone(confidence: number): "positive" | "warning" | "negative" {
  if (confidence >= 0.8) return "positive";
  if (confidence >= 0.5) return "warning";
  return "negative";
}

/** Per-field confidence keys the identification contract defines. */
const FIELD_LABELS: Record<string, string> = {
  series: "Series",
  year: "Year",
  mint: "Mint",
  metal: "Metal",
  finish: "Finish",
  edge: "Edge",
  country: "Country",
  fineness: "Fineness",
  denomination: "Denomination",
  sizeEstimateTroyOz: "Size (ozt)",
};

function fieldLabel(name: string): string {
  return FIELD_LABELS[name] ?? name;
}

function FieldChip({ name, confidence }: { name: string; confidence: number }) {
  return (
    <div className="flex flex-col gap-0.5 rounded-md border border-border bg-surface-raised/50 px-2.5 py-1.5">
      <span className="text-[0.688rem] tracking-wide text-ink-muted uppercase">
        {fieldLabel(name)}
      </span>
      <span
        className={cn(
          "tnum text-sm font-semibold",
          confidenceTone(confidence) === "positive" && "text-positive",
          confidenceTone(confidence) === "warning" && "text-warning",
          confidenceTone(confidence) === "negative" && "text-negative",
        )}
      >
        {Math.round(confidence * 100)}%
      </span>
    </div>
  );
}

/** Candidates carry ids + scores only — names resolve from the catalog. */
function CandidateRow({
  coinTypeId,
  score,
  index,
  confirmed,
  confirmDisabled,
  onConfirm,
}: {
  coinTypeId: number;
  score: number;
  index: number;
  confirmed: boolean;
  confirmDisabled: boolean;
  onConfirm: (coinTypeId: number) => void;
}) {
  const coinTypeQuery = useQuery({
    queryKey: ["catalog", "coinType", coinTypeId],
    queryFn: () => api.catalog.coinType(coinTypeId),
    staleTime: 5 * 60_000,
  });

  return (
    <li
      className={cn(
        "flex items-center justify-between gap-3 rounded-md border border-border bg-surface-raised/50 px-3 py-2",
        confirmed && "border-positive/60",
      )}
    >
      <div className="flex min-w-0 items-baseline gap-3">
        <span className="tnum text-xs text-ink-muted">#{index + 1}</span>
        <span className="truncate text-sm font-medium text-ink">
          {coinTypeQuery.isPending ? (
            <Skeleton className="h-4 w-44" />
          ) : coinTypeQuery.isError ? (
            <>Catalog type #{coinTypeId}</>
          ) : (
            coinTypeQuery.data.detail.name
          )}
        </span>
        <span className="tnum text-xs text-ink-muted">{Math.round(score * 100)}% match</span>
      </div>
      {confirmed ? (
        <Badge tone="positive">confirmed</Badge>
      ) : (
        <Button
          size="sm"
          variant="goldAccent"
          disabled={confirmDisabled}
          onClick={() => onConfirm(coinTypeId)}
        >
          Confirm
        </Button>
      )}
    </li>
  );
}

export function IdentifyPanel({ className }: { className?: string }) {
  const [obverse, setObverse] = useShot();
  const [reverse, setReverse] = useShot();
  const [jobId, setJobId] = useState<number | null>(null);
  const queryClient = useQueryClient();

  const submit = useMutation({
    mutationFn: () =>
      api.identification.submit({
        obverse: obverse?.file as File,
        reverse: reverse?.file as File,
      }),
    onSuccess: (result) => setJobId(result.jobId),
  });

  const jobQuery = useQuery({
    queryKey: ["identification", jobId],
    queryFn: () => api.identification.status(jobId as number),
    enabled: jobId !== null,
    refetchInterval: (query) =>
      identificationStatusPolling(query.state.data?.status) ? 2000 : false,
  });

  const confirm = useMutation({
    mutationFn: (coinTypeId: number) => api.identification.confirm(jobId as number, coinTypeId),
    // Confirm answers 204 No Content — refetch the status to pick up the decision.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["identification", jobId] });
    },
  });

  const job: IdentificationStatusResponse | undefined = jobQuery.data;
  const bothShots = obverse !== null && reverse !== null;
  const statusDone = job != null && !identificationStatusPolling(job.status);

  return (
    <div className={cn("flex flex-col gap-4", className)}>
      <Card>
        <CardHeader>
          <CardTitle>Identify a coin</CardTitle>
          <p className="text-sm text-ink-muted">
            Photograph both sides — the obverse legend and reverse design are matched against the
            catalog together.
          </p>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap items-start justify-center gap-8 sm:justify-start">
            <ShotSlot side="obverse" shot={obverse} onSelect={setObverse} />
            <ShotSlot side="reverse" shot={reverse} onSelect={setReverse} />
          </div>

          {submit.isError ? (
            <p role="alert" className="text-sm text-negative">
              {submit.error instanceof ApiError
                ? `Submission failed (HTTP ${submit.error.status}). Both sides are required (10 KB–15 MB each).`
                : "Submission failed — the API could not be reached."}
            </p>
          ) : null}

          <div>
            <Button
              variant="goldAccent"
              disabled={!bothShots || submit.isPending || jobId !== null}
              onClick={() => submit.mutate()}
            >
              {submit.isPending ? "Submitting…" : jobId ? "Job submitted" : "Identify"}
            </Button>
            {!bothShots ? (
              <span className="ml-3 text-xs text-ink-muted">
                Both sides are required before submission.
              </span>
            ) : null}
          </div>
        </CardContent>
      </Card>

      {jobId != null ? (
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2">
            <CardTitle>Job {jobId}</CardTitle>
            <div className="flex items-center gap-2">
              {job ? <Badge tone="neutral">{job.providerLabel}</Badge> : null}
              {job ? (
                <Badge
                  tone={
                    job.status === 2 ? "positive" : job.status === 3 ? "negative" : "warning"
                  }
                >
                  {identificationStatusLabel(job.status)}
                </Badge>
              ) : null}
            </div>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {jobQuery.isPending ? (
              <div className="flex flex-col gap-2">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-20 w-full" />
              </div>
            ) : jobQuery.isError ? (
              <p className="text-sm text-ink-muted">
                Job status unavailable — the API could not be reached.
              </p>
            ) : job ? (
              <>
                {job.status === 3 ? (
                  <p className="text-sm text-negative">
                    Identification failed. Re-shoot the photos and submit again.
                  </p>
                ) : null}

                {Object.keys(job.perFieldConfidences).length > 0 ? (
                  <section aria-label="Per-field confidence">
                    <h4 className="mb-2 text-xs font-medium tracking-wide text-ink-muted uppercase">
                      Field confidences
                    </h4>
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
                      {Object.entries(job.perFieldConfidences).map(([name, confidence]) => (
                        <FieldChip key={name} name={name} confidence={confidence} />
                      ))}
                    </div>
                  </section>
                ) : statusDone ? null : (
                  <p className="text-sm text-ink-muted">Waiting for field results…</p>
                )}

                {job.candidates.length > 0 ? (
                  <section aria-label="Candidate matches">
                    <h4 className="mb-2 text-xs font-medium tracking-wide text-ink-muted uppercase">
                      Candidates — confirm the right one
                    </h4>
                    <ul className="flex flex-col gap-2">
                      {job.candidates.map((candidate, index) => (
                        <CandidateRow
                          key={candidate.coinTypeId}
                          coinTypeId={candidate.coinTypeId}
                          score={candidate.score}
                          index={index}
                          confirmed={job.confirmedCoinTypeId === candidate.coinTypeId}
                          confirmDisabled={
                            confirm.isPending || job.confirmedCoinTypeId !== null
                          }
                          onConfirm={(coinTypeId) => confirm.mutate(coinTypeId)}
                        />
                      ))}
                    </ul>
                    {confirm.isError ? (
                      <p role="alert" className="mt-2 text-xs text-negative">
                        Confirmation failed — the API could not be reached.
                      </p>
                    ) : null}
                  </section>
                ) : null}
              </>
            ) : null}
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
