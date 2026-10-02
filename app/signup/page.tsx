"use client"

import { useState } from "react"
import Link from "next/link"
import { Loader2, Mail, Lock, User } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Field, FieldGroup, FieldLabel, FieldDescription } from "@/components/ui/field"
import { InputGroup, InputGroupInput, InputGroupAddon } from "@/components/ui/input-group"
import { Separator } from "@/components/ui/separator"
import { GoogleIcon } from "@/components/auth/google-icon"
import { AuthShell } from "@/components/auth/auth-shell"
import { register } from "@/lib/auth"

export default function SignupPage() {
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const data = new FormData(e.currentTarget)
    const name = String(data.get("name") ?? "").trim()
    const email = String(data.get("email") ?? "").trim()
    const password = String(data.get("password") ?? "")

    if (!name || !email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || password.length < 8) {
      toast.error("Check your details", {
        description: "Fill in all fields and use a valid email plus a password with at least 8 characters.",
      })
      return
    }

    setSubmitting(true)
    try {
      const auth = await register(name, email, password)
      toast.success("Account created", {
        description: `Welcome, ${auth.user.name.split(" ")[0]}. Your workspace is ready.`,
      })
      window.location.href = "/events"
    } catch (err: any) {
      toast.error("Registration failed", {
        description: err.message || "Please try again.",
      })
      setSubmitting(false)
    }
  }

  return (
    <AuthShell
      title="Create your account"
      description="Start issuing verifiable certificates for your events."
      footer={
        <>
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-primary hover:underline">
            Log in
          </Link>
        </>
      }
    >
      <form className="flex flex-col gap-5" onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="name">Full name</FieldLabel>
            <InputGroup>
              <InputGroupAddon>
                <User data-icon="inline-start" />
              </InputGroupAddon>
              <InputGroupInput
                id="name"
                name="name"
                autoComplete="name"
                placeholder="Jane Cooper"
                required
                disabled={submitting}
              />
            </InputGroup>
          </Field>

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
            <FieldLabel htmlFor="password">Password</FieldLabel>
            <InputGroup>
              <InputGroupAddon>
                <Lock data-icon="inline-start" />
              </InputGroupAddon>
              <InputGroupInput
                id="password"
                name="password"
                type="password"
                autoComplete="new-password"
                minLength={8}
                placeholder="••••••••"
                required
                disabled={submitting}
              />
            </InputGroup>
            <FieldDescription>Use at least 8 characters.</FieldDescription>
          </Field>
        </FieldGroup>

        <Button type="submit" size="lg" className="mt-1 w-full" disabled={submitting}>
          {submitting ? (
            <>
              <Loader2 data-icon="inline-start" className="animate-spin" />
              Creating account…
            </>
          ) : (
            "Create account"
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

        <p className="text-center text-xs leading-relaxed text-muted-foreground">
          By creating an account you agree to our Terms of Service and Privacy Policy.
        </p>
      </form>
    </AuthShell>
  )
}
