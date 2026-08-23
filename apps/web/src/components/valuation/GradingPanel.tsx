import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";

/**
 * Grading facts for a holding. The current API surface carries no grading
 * data (the seeded collection is raw), so this renders the honest empty state
 * until grading joins the DTOs.
 */
export interface GradingPanelProps {
  className?: string;
}

export function GradingPanel({ className }: GradingPanelProps) {
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
