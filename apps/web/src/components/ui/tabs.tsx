"use client";

/** Radix Tabs wrapper — roving focus and arrow-key navigation are built in. */
import * as TabsPrimitive from "@radix-ui/react-tabs";
import { cn } from "@/lib/cn";

export const Tabs = TabsPrimitive.Root;

export function TabsList({ className, ...props }: TabsPrimitive.TabsListProps) {
  return (
    <TabsPrimitive.List
      className={cn(
        "inline-flex items-center gap-1 rounded-lg bg-surface-raised p-1",
        className,
      )}
      {...props}
    />
  );
}

export function TabsTrigger({ className, ...props }: TabsPrimitive.TabsTriggerProps) {
  return (
    <TabsPrimitive.Trigger
      className={cn(
        "inline-flex h-8 items-center justify-center rounded-md px-3 text-sm font-medium text-ink-muted",
        "transition-colors hover:text-ink",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
        "data-[state=active]:bg-surface data-[state=active]:text-ink",
        className,
      )}
      {...props}
    />
  );
}

export function TabsContent({ className, ...props }: TabsPrimitive.TabsContentProps) {
  return (
    <TabsPrimitive.Content
      className={cn("mt-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus", className)}
      {...props}
    />
  );
}
