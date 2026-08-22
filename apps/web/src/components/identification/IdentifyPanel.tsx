"use client";

/**
 * Identification upload/capture shell. Two-shot obverse+reverse capture,
 * submit → jobId, polling while pending/running, per-field confidence chips,
 * ranked candidates with confirm buttons, and an explicit "offline evaluator"
 * label when provider === 'offline'. No request ever blocks on a model call.
 */
import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@/lib/api";
import type {
  IdentificationFieldName,
  IdentificationProvider,
  IdentifiedField,
} from "@/lib/api-types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, Select } from "@/components/ui/field";
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

const FIELD_LABELS: Record<IdentificationFieldName, string> = {
  series: "Series",
  year: "Year",
  mintMark: "Mint mark",
  finishPrimary: "Finish",
  metal: "Metal",
};

function FieldChip({ name, field }: { name: IdentificationFieldName; field: IdentifiedField }) {
  return (
    <div className="flex flex-col gap-0.5 rounded-md border border-border bg-surface-raised/50 px-2.5 py-1.5">
      <span className="text-[0.688rem] tracking-wide text-ink-muted uppercase">
        {FIELD_LABELS[name]}
      </span>
      <span className="text-sm font-medium text-ink">{field.value}</span>
      <span className="tnum text-[0.688rem] text-ink-muted">
        confidence{" "}
        <span
          className={cn(
            "font-semibold",
            confidenceTone(field.confidence) === "positive" && "text-positive",
            confidenceTone(field.confidence) === "warning" && "text-warning",
            confidenceTone(field.confidence) === "negative" && "text-negative",
          )}
        >
          {Math.round(field.confidence * 100)}%
        </span>
      </span>
    </div>
  );
}

const PROVIDER_LABEL: Record<IdentificationProvider, string> = {
  offline: "offline evaluator",
  openai: "OpenAI vision",
  gemini: "Gemini vision",
};

export function IdentifyPanel({ className }: { className?: string }) {
  const [obverse, setObverse] = useShot();
  const [reverse, setReverse] = useShot();
  const [provider, setProvider] = useState<IdentificationProvider>("offline");
  const [jobId, setJobId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const submit = useMutation({
    mutationFn: () =>
      api.identification.submit({
        obverse: obverse?.file as File,
        reverse: reverse?.file as File,
        provider,
      }),
    onSuccess: (result) => setJobId(result.jobId),
  });

  const jobQuery = useQuery({
    queryKey: ["identification", jobId],
    queryFn: () => api.identification.status(jobId as string),
    enabled: jobId !== null,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === "pending" || status === "running" ? 2000 : false;
    },
  });

  const confirm = useMutation({
    mutationFn: (coinTypeId: string) => api.identification.confirm(jobId as string, coinTypeId),
    onSuccess: (job) => queryClient.setQueryData(["identification", jobId], job),
  });

  const job = jobQuery.data;
  const bothShots = obverse !== null && reverse !== null;

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
            <div className="w-56">
              <Field label="Evaluator" hint="Offline runs fully local — no image leaves the machine.">
                <Select
                  value={provider}
                  onChange={(e) => setProvider(e.target.value as IdentificationProvider)}
                >
                  <option value="offline">offline evaluator</option>
                  <option value="openai">OpenAI vision</option>
                  <option value="gemini">Gemini vision</option>
                </Select>
              </Field>
            </div>
          </div>

          {submit.isError ? (
            <p role="alert" className="text-sm text-negative">
              {submit.error instanceof ApiError
                ? `Submission failed (HTTP ${submit.error.status}). Is the API running?`
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

      {jobId ? (
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-2">
            <CardTitle>Job {jobId}</CardTitle>
            <div className="flex items-center gap-2">
              {job ? (
                <Badge tone="neutral">{PROVIDER_LABEL[job.provider]}</Badge>
              ) : null}
              {job?.status === "pending" || job?.status === "running" ? (
                <Badge tone="warning">{job?.status}</Badge>
              ) : job?.status === "complete" ? (
                <Badge tone="positive">complete</Badge>
              ) : job?.status === "failed" ? (
                <Badge tone="negative">failed</Badge>
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
                {job.status === "failed" ? (
                  <p className="text-sm text-negative">
                    Identification failed. Re-shoot the photos and submit again.
                  </p>
                ) : null}

                {Object.entries(job.fields).length > 0 ? (
                  <section aria-label="Detected fields">
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
                      {(Object.entries(job.fields) as Array<[IdentificationFieldName, IdentifiedField]>).map(
                        ([name, field]) => <FieldChip key={name} name={name} field={field} />,
                      )}
                    </div>
                  </section>
                ) : job.status !== "failed" ? (
                  <p className="text-sm text-ink-muted">Waiting for field results…</p>
                ) : null}

                {job.candidates.length > 0 ? (
                  <section aria-label="Candidate matches">
                    <h4 className="mb-2 text-xs font-medium tracking-wide text-ink-muted uppercase">
                      Candidates — confirm the right one
                    </h4>
                    <ul className="flex flex-col gap-2">
                      {job.candidates.map((candidate, index) => {
                        const confirmed = job.confirmedCoinTypeId === candidate.coinTypeId;
                        return (
                          <li
                            key={candidate.coinTypeId}
                            className={cn(
                              "flex items-center justify-between gap-3 rounded-md border border-border bg-surface-raised/50 px-3 py-2",
                              confirmed && "border-positive/60",
                            )}
                          >
                            <div className="flex min-w-0 items-baseline gap-3">
                              <span className="tnum text-xs text-ink-muted">#{index + 1}</span>
                              <span className="truncate text-sm font-medium text-ink">
                                {candidate.seriesName} · {candidate.year}
                              </span>
                              <span className="tnum text-xs text-ink-muted">
                                {Math.round(candidate.score * 100)}% match
                              </span>
                            </div>
                            {confirmed ? (
                              <Badge tone="positive">confirmed</Badge>
                            ) : job.confirmedCoinTypeId ? null : (
                              <Button
                                size="sm"
                                variant="goldAccent"
                                disabled={confirm.isPending}
                                onClick={() => confirm.mutate(candidate.coinTypeId)}
                              >
                                Confirm
                              </Button>
                            )}
                          </li>
                        );
                      })}
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
