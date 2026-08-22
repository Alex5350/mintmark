import type { Metadata } from "next";
import { HoldingDetail } from "@/components/holdings/HoldingDetail";

export const metadata: Metadata = { title: "Holding" };

export default async function HoldingPage({ params }: PageProps<"/holdings/[id]">) {
  const { id } = await params;
  return <HoldingDetail holdingId={id} />;
}
