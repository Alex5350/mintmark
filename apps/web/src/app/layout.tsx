import type { Metadata } from "next";
// Self-hosted variable fonts (no Google Fonts fetch at build time).
// Fraunces → headings ("engraved legend" serif), Inter → body/UI.
import "@fontsource-variable/fraunces";
import "@fontsource-variable/inter";
import { Providers } from "@/providers";
import { SiteHeader } from "@/components/layout/SiteHeader";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Mintmark",
    template: "%s — Mintmark",
  },
  description:
    "Precious-metals portfolio tracker: melt and collectible valuations with full spot provenance.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // suppressHydrationWarning: next-themes sets the class before hydration.
    <html lang="en" className="h-full antialiased" suppressHydrationWarning>
      <body className="flex min-h-full flex-col bg-base font-body text-ink">
        <Providers>
          <SiteHeader />
          <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">{children}</main>
          <footer className="border-t border-border py-4">
            <p className="mx-auto max-w-6xl px-4 text-xs text-ink-muted">
              Mintmark — melt values use actual metal weight; stale spot prices are always badged,
              never silent.
            </p>
          </footer>
        </Providers>
      </body>
    </html>
  );
}
