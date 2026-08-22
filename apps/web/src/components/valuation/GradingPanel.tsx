import type { Grading } from "@/lib/api-types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/ui/empty-state";

/** Grading facts for a holding — service, grade, designation, cert number. */
export interface GradingPanelProps {
  grading?: Grading | null;
  className?: string;
}

export function GradingPanel({ grading, className }: GradingPanelProps) {
  if (!grading) {
    return (
      <Card className={className}>
        <CardHeader>
          <CardTitle>Grading</CardTitle>
        </CardHeader>
        <CardContent>
          <EmptyState
            title="Raw — not graded"
            description="Once this coin is slabbed, its service, grade, and cert number live here."
            className="border-dashed py-6"
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>Grading</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap items-center gap-x-6 gap-y-3">
        <Badge tone="neutral" className="text-sm">
          {grading.service}
        </Badge>
        <div>
          <div className="text-xs text-ink-muted uppercase">Grade</div>
          <div className="tnum text-xl font-semibold text-ink">{grading.grade}</div>
        </div>
        {grading.designation ? (
          <div>
            <div className="text-xs text-ink-muted uppercase">Designation</div>
            <div className="text-sm font-medium text-ink">{grading.designation}</div>
          </div>
        ) : null}
        {grading.certNumber ? (
          <div>
            <div className="text-xs text-ink-muted uppercase">Cert</div>
            <div className="tnum font-heading text-sm text-ink">{grading.certNumber}</div>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
