import type { Metadata } from "next";
import { Suspense } from "react";
import { AllocationChart } from "@/components/dashboard/AllocationChart";
import { RecentHoldings } from "@/components/dashboard/RecentHoldings";
import { RollupCards } from "@/components/dashboard/RollupCards";
import { TopSeriesList } from "@/components/dashboard/TopSeriesList";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

export const metadata: Metadata = { title: "Dashboard" };

/**
 * Server-component shell; data sections are client components ("use client"
 * lives in each section file) streamed in under Suspense with skeleton
 * fallbacks.
 */
export default function DashboardPage() {
  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="font-heading text-2xl font-semibold text-ink">Dashboard</h1>
        <p className="text-sm text-ink-muted">
          Portfolio totals refresh every 30 seconds while the tab is open.
        </p>
      </header>

      <Suspense fallback={<Skeleton className="h-28 w-full" />}>
        <RollupCards />
      </Suspense>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Allocation by metal</CardTitle>
          </CardHeader>
          <CardContent>
            <Suspense fallback={<Skeleton className="h-64 w-full" />}>
              <AllocationChart />
            </Suspense>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Top series</CardTitle>
          </CardHeader>
          <CardContent>
            <Suspense fallback={<Skeleton className="h-64 w-full" />}>
              <TopSeriesList />
            </Suspense>
          </CardContent>
        </Card>
      </div>

      <section aria-label="Recent holdings" className="flex flex-col gap-4">
        <h2 className="font-heading text-lg font-semibold text-ink">Recent holdings</h2>
        <Suspense fallback={<Skeleton className="h-36 w-full" />}>
          <RecentHoldings />
        </Suspense>
      </section>
    </div>
  );
}
