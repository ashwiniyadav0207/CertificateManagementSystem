"use client"

import { useState } from "react"
import Link from "next/link"
import { Loader2, Mail, Lock } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { InputGroup, InputGroupInput, InputGroupAddon } from "@/components/ui/input-group"
import { Separator } from "@/components/ui/separator"
import { GoogleIcon } from "@/components/auth/google-icon"
import { AuthShell } from "@/components/auth/auth-shell"
import { login } from "@/lib/auth"

export default function LoginPage() {
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const data = new FormData(e.currentTarget)
    const email = String(data.get("email") ?? "").trim()
    const password = String(data.get("password") ?? "")

    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || password.length < 8) {
      toast.error("Check your details", {
        description: "Use a valid email and a password with at least 8 characters.",
      })
      return
    }

    setSubmitting(true)
    try {
      const auth = await login(email, password)
      toast.success("Welcome back", {
        description: `Signed in as ${auth.user.name}.`,
      })
      window.location.href = "/events"
    } catch (err: any) {
      toast.error("Login failed", {
        description: err.message || "Please check your credentials and try again.",
      })
      setSubmitting(false)
    }
  }

  return (
    <AuthShell
      title="Welcome back"
      description="Log in to manage your events and credentials."
      footer={
        <>
          Don&apos;t have an account?{" "}
          <Link href="/signup" className="font-medium text-primary hover:underline">
            Create one
          </Link>
        </>
      }
    >
      <form className="flex flex-col gap-5" onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="email">Email</FieldLabel>
            <InputGroup>
              <InputGroupAddon>
                <Mail data-icon="inline-start" />
              </InputGroupAddon>
              <InputGroupInput
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                placeholder="you@company.com"
                required
                disabled={submitting}
              />
            </InputGroup>
          </Field>

          <Field>
            <div className="flex items-center justify-between">
              <FieldLabel htmlFor="password">Password</FieldLabel>
              <Link href="/forgot-password" className="text-xs font-medium text-primary hover:underline">
                Forgot password?
              </Link>
            </div>
            <InputGroup>
              <InputGroupAddon>
                <Lock data-icon="inline-start" />
              </InputGroupAddon>
              <InputGroupInput
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                placeholder="••••••••"
                required
                disabled={submitting}
              />
            </InputGroup>
          </Field>
        </FieldGroup>

        <Button type="submit" size="lg" className="mt-1 w-full" disabled={submitting}>
          {submitting ? (
            <>
              <Loader2 data-icon="inline-start" className="animate-spin" />
              Logging in…
            </>
          ) : (
            "Log in"
          )}
        </Button>

        <div className="flex items-center gap-3">
          <Separator className="flex-1" />
          <span className="text-xs text-muted-foreground">OR</span>
          <Separator className="flex-1" />
        </div>

        <Button type="button" variant="outline" size="lg" className="w-full" disabled={submitting}>
          <GoogleIcon className="size-4" data-icon="inline-start" />
          Continue with Google
        </Button>
      </form>
    </AuthShell>
  )
}
