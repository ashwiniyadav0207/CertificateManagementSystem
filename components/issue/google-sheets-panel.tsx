"use client"

import { useState } from "react"
import { CheckCircle2, Link2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Field, FieldLabel, FieldDescription } from "@/components/ui/field"

const SheetsIcon = () => (
  <svg viewBox="0 0 48 48" className="size-5" aria-hidden="true">
    <path fill="#0F9D58" d="M8 4h22l10 10v30a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z" />
    <path fill="#087F45" d="M30 4v10h10Z" />
    <path fill="#fff" d="M14 22h20v2H14zm0 5h20v2H14zm0 5h20v2H14zm0 5h20v2H14z" opacity=".9" />
    <path fill="#0F9D58" d="M14 22h5v18h-5zm7.5 0h5v18h-5z" opacity="0" />
  </svg>
)

export function GoogleSheetsPanel() {
  const [connected, setConnected] = useState(false)
  const [sheetUrl, setSheetUrl] = useState("")

  if (connected) {
    return (
      <div className="flex items-center gap-3 rounded-xl border border-success/40 bg-success/10 p-4">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-success/15 text-success">
          <CheckCircle2 className="size-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-foreground">Recipients — Cohort 2026</p>
          <p className="text-xs text-muted-foreground">128 recipients synced from Google Sheets</p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={() => setConnected(false)}>
          Disconnect
        </Button>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-4 rounded-xl border border-border bg-muted/40 p-6">
      <div className="flex items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-background ring-1 ring-border">
          <SheetsIcon />
        </span>
        <div>
          <p className="text-sm font-medium text-foreground">Connect Google Sheets</p>
          <p className="text-xs text-muted-foreground">Sync recipients directly from a shared sheet.</p>
        </div>
      </div>
      <Field>
        <FieldLabel htmlFor="sheet-url">Sheet URL</FieldLabel>
        <Input
          id="sheet-url"
          placeholder="https://docs.google.com/spreadsheets/d/..."
          value={sheetUrl}
          onChange={(e) => setSheetUrl(e.target.value)}
        />
        <FieldDescription>Make sure the sheet has Name and Email columns and is shared as viewable.</FieldDescription>
      </Field>
      <Button type="button" onClick={() => setConnected(true)} disabled={!sheetUrl.trim()} className="self-start">
        <Link2 data-icon="inline-start" />
        Connect sheet
      </Button>
    </div>
  )
}
