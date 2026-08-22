import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger" | "goldAccent";
export type ButtonSize = "sm" | "md" | "lg";

const baseStyles =
  "inline-flex select-none items-center justify-center gap-2 rounded-md font-medium transition-colors " +
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus focus-visible:ring-offset-2 focus-visible:ring-offset-base " +
  "disabled:pointer-events-none disabled:opacity-50";

const variantStyles: Record<ButtonVariant, string> = {
  // Ink-on-base inversion stays readable in both themes.
  primary: "bg-ink text-base hover:opacity-90",
  secondary: "border border-border bg-surface text-ink hover:bg-surface-raised",
  ghost: "text-ink-muted hover:bg-surface-raised hover:text-ink",
  danger: "bg-negative text-white hover:opacity-90",
  // Semantic gold accent — used sparingly for confirm/primary-collector actions.
  goldAccent: "border border-gold/60 bg-gold/10 text-gold hover:bg-gold/20",
};

const sizeStyles: Record<ButtonSize, string> = {
  sm: "h-8 px-3 text-xs",
  md: "h-10 px-4 text-sm",
  lg: "h-12 px-6 text-base",
};

export function buttonStyles(options?: {
  variant?: ButtonVariant;
  size?: ButtonSize;
  className?: string;
}): string {
  const { variant = "primary", size = "md", className } = options ?? {};
  return cn(baseStyles, variantStyles[variant], sizeStyles[size], className);
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

export function Button({ variant, size, className, type = "button", ...props }: ButtonProps) {
  return <button type={type} className={buttonStyles({ variant, size, className })} {...props} />;
}
