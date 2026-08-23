"use client";

/** Shared login/register form. Errors are honest — no backend means a clear
 * "cannot reach the API" message, never a fake session. */
import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { useAuthStore } from "@/lib/auth-store";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return "Invalid email or password.";
    if (error.status === 409) return "An account with this email already exists.";
    if (error.status === 400) return "The API rejected the request — check the fields.";
    return `The API answered with HTTP ${error.status}.`;
  }
  if (error instanceof TypeError) {
    return "Cannot reach the Mintmark API. Is the backend running?";
  }
  return "Something went wrong signing in.";
}

export function AuthForm({ mode }: { mode: "login" | "register" }) {
  const router = useRouter();
  const setTokens = useAuthStore((s) => s.setTokens);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const isRegister = mode === "register";

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setError(null);
    try {
      const tokens = isRegister
        ? await api.auth.register({
            email,
            password,
            displayName: displayName.trim() || undefined,
          })
        : await api.auth.login(email, password);
      setTokens(tokens);
      router.push("/dashboard");
    } catch (err) {
      setError(describeError(err));
    } finally {
      setPending(false);
    }
  }

  return (
    <Card className="mx-auto w-full max-w-sm">
      <CardHeader>
        <CardTitle>{isRegister ? "Create your account" : "Sign in"}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {isRegister ? (
            <Field label="Display name" hint="Optional — shown next to your collection.">
              <Input
                autoComplete="name"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
              />
            </Field>
          ) : null}
          <Field label="Email">
            <Input
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </Field>
          <Field label="Password">
            <Input
              type="password"
              autoComplete={isRegister ? "new-password" : "current-password"}
              required
              minLength={8}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </Field>

          {error ? (
            <p role="alert" className="text-sm text-negative">
              {error}
            </p>
          ) : null}

          <Button type="submit" disabled={pending}>
            {pending ? "Working…" : isRegister ? "Create account" : "Sign in"}
          </Button>

          <p className="text-center text-sm text-ink-muted">
            {isRegister ? "Already have an account? " : "New to Mintmark? "}
            <Link
              href={isRegister ? "/login" : "/register"}
              className="text-ink underline underline-offset-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus"
            >
              {isRegister ? "Sign in" : "Create an account"}
            </Link>
          </p>
        </form>
      </CardContent>
    </Card>
  );
}
