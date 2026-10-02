"use client"

import { useCallback, useRef, useState } from "react"
import { FileSpreadsheet, CheckCircle2, TriangleAlert, X } from "lucide-react"
import { toast } from "sonner"
import Folder from "@/components/effects/folder"
import { Button } from "@/components/ui/button"

export interface ParsedRecipient {
  name: string
  email?: string
}

interface CsvDropzoneProps {
  onFileAccepted: (file: File, recipients: ParsedRecipient[]) => void
  onReset?: () => void
}

interface CsvSummary {
  fileName: string
  rowCount: number | null
  error: string | null
  sampleNames: string[]
}

function parseCsv(text: string): { recipients: ParsedRecipient[]; error: string | null } {
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0)
  if (lines.length === 0) {
    return { recipients: [], error: "The file is completely empty." }
  }

  // Parse header
  const headerParts = lines[0].split(",").map((col) => col.trim().replace(/^["']|["']$/g, "").toLowerCase())
  const nameIndex = headerParts.findIndex((h) => h.includes("name") || h.includes("participant") || h.includes("student"))
  const emailIndex = headerParts.findIndex((h) => h.includes("email") || h.includes("mail"))

  if (nameIndex === -1) {
    return {
      recipients: [],
      error: 'CSV header must include a "Name" column (e.g., Name, Email).',
    }
  }

  const recipients: ParsedRecipient[] = []
  for (let i = 1; i < lines.length; i++) {
    const rawLine = lines[i].trim()
    if (!rawLine) continue

    // Handle basic quoted or unquoted CSV values
    const cols: string[] = []
    let current = ""
    let inQuotes = false

    for (let charIdx = 0; charIdx < rawLine.length; charIdx++) {
      const c = rawLine[charIdx]
      if (c === '"') {
        inQuotes = !inQuotes
      } else if (c === ',' && !inQuotes) {
        cols.push(current.trim())
        current = ""
      } else {
        current += c
      }
    }
    cols.push(current.trim())

    const name = cols[nameIndex]?.replace(/^["']|["']$/g, "").trim()
    const email = emailIndex >= 0 ? cols[emailIndex]?.replace(/^["']|["']$/g, "").trim() : undefined

    if (name) {
      recipients.push({ name, email: email || undefined })
    }
  }

  if (recipients.length === 0) {
    return { recipients: [], error: "No recipient rows found below the header." }
  }

  return { recipients, error: null }
}

export function CsvDropzone({ onFileAccepted, onReset }: CsvDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragActive, setDragActive] = useState(false)
  const [summary, setSummary] = useState<CsvSummary | null>(null)

  const processFile = useCallback(
    (file: File) => {
      if (!file.name.toLowerCase().endsWith(".csv")) {
        setSummary({
          fileName: file.name,
          rowCount: null,
          error: "Only standard .csv files are supported.",
          sampleNames: [],
        })
        toast.error("Unsupported file", {
          description: "Please save your spreadsheet as a CSV (.csv) file.",
        })
        return
      }

      const reader = new FileReader()
      reader.onload = () => {
        const text = String(reader.result ?? "")
        const { recipients, error } = parseCsv(text)

        if (error) {
          setSummary({
            fileName: file.name,
            rowCount: null,
            error,
            sampleNames: [],
          })
          toast.error("CSV formatting error", { description: error })
        } else {
          setSummary({
            fileName: file.name,
            rowCount: recipients.length,
            error: null,
            sampleNames: recipients.slice(0, 3).map((r) => r.name),
          })
          onFileAccepted(file, recipients)
          toast.success("CSV Parsed Successfully", {
            description: `${recipients.length} recipients ready for batch certificate issuance.`,
          })
        }
      }

      reader.onerror = () => {
        setSummary({
          fileName: file.name,
          rowCount: null,
          error: "Failed to read file.",
          sampleNames: [],
        })
      }

      reader.readAsText(file)
    },
    [onFileAccepted]
  )

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setDragActive(false)
    const file = e.dataTransfer.files?.[0]
    if (file) processFile(file)
  }

  function reset() {
    setSummary(null)
    if (inputRef.current) inputRef.current.value = ""
    onReset?.()
  }

  if (summary) {
    return (
      <div
        className={`flex items-start gap-3 rounded-xl border p-4 shadow-sm transition-colors ${
          summary.error ? "border-destructive/40 bg-destructive/10" : "border-success/40 bg-success/10"
        }`}
      >
        <span
          className={`flex size-10 shrink-0 items-center justify-center rounded-lg mt-0.5 ${
            summary.error ? "bg-destructive/15 text-destructive" : "bg-success/15 text-success"
          }`}
        >
          {summary.error ? <TriangleAlert className="size-5" /> : <CheckCircle2 className="size-5" />}
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-foreground">{summary.fileName}</p>
          <p className="text-xs text-muted-foreground mt-0.5">
            {summary.error ?? (
              <span>
                <strong className="text-foreground">{summary.rowCount}</strong> recipients parsed.
                {summary.sampleNames.length > 0 && ` Samples: ${summary.sampleNames.join(", ")}...`}
              </span>
            )}
          </p>
        </div>
        <Button type="button" variant="ghost" size="icon-sm" onClick={reset} aria-label="Remove file">
          <X className="size-4" />
        </Button>
      </div>
    )
  }

  return (
    <div
      onDragOver={(e) => {
        e.preventDefault()
        setDragActive(true)
      }}
      onDragLeave={() => setDragActive(false)}
      onDrop={handleDrop}
      onClick={() => inputRef.current?.click()}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault()
          inputRef.current?.click()
        }
      }}
      role="button"
      tabIndex={0}
      className={`flex cursor-pointer flex-col items-center justify-center gap-4 rounded-xl border-2 border-dashed p-10 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
        dragActive ? "border-primary bg-primary/5" : "border-border bg-muted/40 hover:border-primary/40"
      }`}
    >
      <div className="pointer-events-none flex h-24 items-center justify-center">
        <Folder color="#a8461f" size={0.9} open={dragActive} items={[<FileSpreadsheet key="f" className="size-4 text-success" />]} />
      </div>
      <div>
        <p className="text-sm font-medium text-foreground">
          Drop your <span className="text-primary underline underline-offset-2">CSV</span> file here
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          or click to select &mdash; first row requires a <strong>Name</strong> column (Email optional)
        </p>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept=".csv"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) processFile(file)
        }}
      />
    </div>
  )
}
