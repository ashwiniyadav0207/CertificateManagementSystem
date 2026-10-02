"use client"

import { useEffect, useState } from "react"
import { Plus, Trash2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

interface Recipient {
  id: string
  name: string
  email: string
}

let idCounter = 0
function makeId() {
  idCounter += 1
  return `recipient-${idCounter}`
}

export function ManualRecipients({
  onValidityChange,
}: {
  onValidityChange?: (valid: boolean) => void
}) {
  const [recipients, setRecipients] = useState<Recipient[]>([
    { id: makeId(), name: "", email: "" },
  ])

  const hasCompleteRow = recipients.some((r) => r.name.trim() && /.+@.+\..+/.test(r.email.trim()))

  useEffect(() => {
    onValidityChange?.(hasCompleteRow)
  }, [hasCompleteRow, onValidityChange])

  function updateRecipient(id: string, key: "name" | "email", value: string) {
    setRecipients((prev) => prev.map((r) => (r.id === id ? { ...r, [key]: value } : r)))
  }

  function addRow() {
    setRecipients((prev) => [...prev, { id: makeId(), name: "", email: "" }])
  }

  function removeRow(id: string) {
    setRecipients((prev) => (prev.length > 1 ? prev.filter((r) => r.id !== id) : prev))
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="rounded-xl border border-border">
        <div className="grid grid-cols-[1fr_1fr_auto] gap-3 border-b border-border bg-muted/40 px-4 py-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
          <span>Name</span>
          <span>Email</span>
          <span className="sr-only">Remove</span>
        </div>
        <div className="flex flex-col divide-y divide-border">
          {recipients.map((recipient, index) => (
            <div key={recipient.id} className="grid grid-cols-[1fr_1fr_auto] items-center gap-3 px-4 py-2.5">
              <Input
                placeholder="Jordan Ellery"
                value={recipient.name}
                onChange={(e) => updateRecipient(recipient.id, "name", e.target.value)}
                aria-label={`Recipient ${index + 1} name`}
              />
              <Input
                type="email"
                placeholder="jordan@example.com"
                value={recipient.email}
                onChange={(e) => updateRecipient(recipient.id, "email", e.target.value)}
                aria-label={`Recipient ${index + 1} email`}
              />
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                onClick={() => removeRow(recipient.id)}
                aria-label="Remove recipient"
              >
                <Trash2 />
              </Button>
            </div>
          ))}
        </div>
      </div>
      <Button type="button" variant="outline" size="sm" onClick={addRow} className="self-start">
        <Plus data-icon="inline-start" />
        Add recipient
      </Button>
    </div>
  )
}
