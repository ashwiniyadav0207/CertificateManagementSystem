"use client"

import Link from "next/link"
import { Suspense, useState, useRef } from "react"
import { useParams, useSearchParams, useRouter } from "next/navigation"
import { ArrowLeft, Loader2, Send, Sheet, FileSpreadsheet, UserPlus } from "lucide-react"
import { toast } from "sonner"
import { AppShell } from "@/components/dashboard/app-shell"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { FieldLabel } from "@/components/ui/field"
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs"
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { CsvDropzone } from "@/components/issue/csv-dropzone"
import { ManualRecipients } from "@/components/issue/manual-recipients"
import { GoogleSheetsPanel } from "@/components/issue/google-sheets-panel"
import { credentialTemplates } from "@/lib/templates"

interface Recipient {
  name: string
  email?: string
}

function IssueContent() {
  const params = useParams<{ id: string }>()
  const searchParams = useSearchParams()
  const router = useRouter()
  const defaultTemplate = searchParams.get("template") ?? credentialTemplates[0].id
  const [templateId, setTemplateId] = useState(defaultTemplate)
  const [consent, setConsent] = useState(false)
  const [recipientsValid, setRecipientsValid] = useState(false)
  const [manualRecipients, setManualRecipients] = useState<Recipient[]>([])
  const [mode, setMode] = useState("manual")
  const [submitting, setSubmitting] = useState(false)
  const manualRef = useRef<HTMLFormElement>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSubmitting(true)

    try {
      // Gather recipients from the active tab
      let recipients: Recipient[] = []
      
      if (mode === "manual") {
        recipients = manualRecipients.filter(r => r.name.trim())
      }

      if (recipients.length === 0) {
        toast.error("No recipients", { description: "Add at least one recipient before issuing." })
        setSubmitting(false)
        return
      }

      // Use batch issue for multiple or single issue for one
      if (recipients.length === 1) {
        const res = await fetch("/api/issue", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ fullName: recipients[0].name, eventName: params.id }),
        })
        if (!res.ok) throw new Error("Issue failed")
        toast.success("Certificate issued", {
          description: `Certificate created for ${recipients[0].name}.`,
        })
      } else {
        const res = await fetch("/api/issue/batch", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ recipients, eventName: params.id }),
        })
        if (!res.ok) throw new Error("Batch issue failed")
        const results = await res.json()
        const succeeded = results.filter((r: any) => r.success).length
        toast.success("Issuance started", {
          description: `${succeeded} of ${recipients.length} certificates created. Recipients will receive an email with a link to their certificate.`,
        })
      }

      router.push(`/events/${params.id}`)
    } catch (err: any) {
      toast.error("Issuance failed", {
        description: err.message || "Something went wrong. Please try again.",
      })
      setSubmitting(false)
    }
  }

  return (
    <AppShell>
      <Link
        href={`/events/${params.id}`}
        className="mb-6 inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft className="size-4" />
        Back to event
      </Link>

      <div className="mb-8 flex flex-col gap-1.5">
        <h1 className="font-serif text-2xl font-semibold text-foreground sm:text-3xl">Issue credentials</h1>
        <p className="max-w-xl text-sm text-muted-foreground">
          Add recipients manually, upload a spreadsheet, or sync a Google Sheet. Each recipient will get an
          emailed link to their certificate on your selected design.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="flex flex-col gap-8">
        <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
          <Tabs value={mode} onValueChange={(v) => setMode(String(v))}>
            <TabsList>
              <TabsTrigger value="manual">
                <UserPlus data-icon="inline-start" />
                Manual
              </TabsTrigger>
              <TabsTrigger value="csv">
                <FileSpreadsheet data-icon="inline-start" />
                Upload CSV
              </TabsTrigger>
              <TabsTrigger value="sheets">
                <Sheet data-icon="inline-start" />
                Google Sheets
              </TabsTrigger>
            </TabsList>
            <TabsContent value="manual" className="mt-5">
              <ManualRecipients onChange={(valid, recs) => {
                setRecipientsValid(valid)
                setManualRecipients(recs)
              }} />
            </TabsContent>
            <TabsContent value="csv" className="mt-5">
              <CsvDropzone onFileAccepted={() => {}} />
            </TabsContent>
            <TabsContent value="sheets" className="mt-5">
              <GoogleSheetsPanel />
            </TabsContent>
          </Tabs>
        </div>

        <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
          <FieldLabel htmlFor="credential-template">Credential template</FieldLabel>
          <Select value={templateId} onValueChange={(v) => setTemplateId(String(v))}>
            <SelectTrigger id="credential-template" className="mt-2 w-full sm:w-80">
              <SelectValue placeholder="Select credential template" />
            </SelectTrigger>
            <SelectContent>
              <SelectGroup>
                {credentialTemplates.map((template) => (
                  <SelectItem key={template.id} value={template.id}>
                    <span
                      className="size-2.5 rounded-full"
                      style={{ backgroundColor: template.accent }}
                    />
                    {template.name}
                  </SelectItem>
                ))}
              </SelectGroup>
            </SelectContent>
          </Select>

          <label className="mt-5 flex items-start gap-2.5 text-sm text-foreground">
            <Checkbox checked={consent} onCheckedChange={(v) => setConsent(Boolean(v))} className="mt-0.5" />
            I have the right to use the personal data of these recipients
          </label>
        </div>

        <div className="flex items-center justify-end gap-3 border-t border-border/70 pt-6">
          <Button type="button" variant="outline" render={<Link href={`/events/${params.id}`} />}>
            Cancel
          </Button>
          <Button type="submit" disabled={!consent || (mode === "manual" && !recipientsValid) || submitting}>
            {submitting ? (
              <>
                <Loader2 data-icon="inline-start" className="animate-spin" />
                Issuing…
              </>
            ) : (
              <>
                <Send data-icon="inline-start" />
                Issue credentials
              </>
            )}
          </Button>
        </div>
      </form>
    </AppShell>
  )
}

export default function IssuePage() {
  return (
    <Suspense>
      <IssueContent />
    </Suspense>
  )
}
