"use client"

import { useState } from "react"
import Link from "next/link"
import { Mail, ArrowLeft, CircleCheck } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { InputGroup, InputGroupInput, InputGroupAddon } from "@/components/ui/input-group"
import { AuthShell } from "@/components/auth/auth-shell"

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false)

  return (
    <AuthShell
      title={sent ? "Check your email" : "Reset your password"}
      description={
        sent
          ? "We sent a password reset link to your inbox. It may take a minute to arrive."
          : "Enter the email tied to your account and we'll send you a reset link."
      }
      footer={
        <Link
          href="/login"
          className="inline-flex items-center gap-1.5 font-medium text-primary hover:underline"
        >
          <ArrowLeft className="size-3.5" />
          Back to log in
        </Link>
      }
    >
      {sent ? (
        <div className="flex flex-col items-center gap-4 py-2 text-center">
          <div className="flex size-12 items-center justify-center rounded-full bg-success/10 text-success">
            <CircleCheck className="size-6" />
          </div>
          <Button variant="outline" className="w-full" onClick={() => setSent(false)}>
            Use a different email
          </Button>
        </div>
      ) : (
        <form
          className="flex flex-col gap-5"
          onSubmit={(e) => {
            e.preventDefault()
            setSent(true)
          }}
        >
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="email">Email</FieldLabel>
              <InputGroup>
                <InputGroupAddon>
                  <Mail data-icon="inline-start" />
                </InputGroupAddon>
                <InputGroupInput id="email" name="email" type="email" placeholder="you@company.com" required />
              </InputGroup>
            </Field>
          </FieldGroup>

          <Button type="submit" size="lg" className="w-full">
            Send reset link
          </Button>
        </form>
      )}
    </AuthShell>
  )
}
