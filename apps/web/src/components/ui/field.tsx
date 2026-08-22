"use client";

import {
  Children,
  cloneElement,
  isValidElement,
  useId,
  type InputHTMLAttributes,
  type ReactElement,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
import { cn } from "@/lib/cn";

const controlStyles =
  "h-10 w-full rounded-md border border-border bg-surface px-3 text-sm text-ink " +
  "placeholder:text-ink-muted transition-colors " +
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus focus-visible:ring-offset-2 focus-visible:ring-offset-base " +
  "aria-invalid:border-negative aria-invalid:ring-negative/40";

export function Input({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cn(controlStyles, className)} {...props} />;
}

export function Select({ className, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={cn(controlStyles, "pr-8", className)} {...props} />;
}

export function Textarea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={cn(controlStyles, "h-auto min-h-20 py-2", className)} {...props} />;
}

export interface FieldProps {
  label: string;
  /** Wires label, hint, and error to the single control child via id/aria. */
  children: ReactElement;
  hint?: string;
  error?: string | null;
  className?: string;
}

/**
 * Label + control + error/hint. The child (Input/Select/Textarea) receives
 * `id`, `aria-invalid`, and `aria-describedby` automatically.
 */
export function Field({ label, children, hint, error, className }: FieldProps) {
  const id = useId();
  const child = Children.only(children);
  if (!isValidElement(child)) {
    throw new Error("Field expects a single element child");
  }
  const messageIds = cn(
    error ? `${id}-error` : undefined,
    hint ? `${id}-hint` : undefined,
  );
  const control = cloneElement(child as ReactElement<Record<string, unknown>>, {
    id,
    "aria-invalid": error ? true : undefined,
    "aria-describedby": messageIds || undefined,
  });

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label htmlFor={id} className="text-sm font-medium text-ink">
        {label}
      </label>
      {control}
      {hint && !error ? (
        <p id={`${id}-hint`} className="text-xs text-ink-muted">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={`${id}-error`} role="alert" className="text-xs text-negative">
          {error}
        </p>
      ) : null}
    </div>
  );
}
