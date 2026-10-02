"use client"

import { useCallback, useRef, useState } from "react"
import { FileSpreadsheet, CheckCircle2, TriangleAlert, X } from "lucide-react"
import { toast } from "sonner"
import Folder from "@/components/effects/folder"
import { Button } from "@/components/ui/button"

interface CsvDropzoneProps {
  onFileAccepted: (file: File) => void
}

interface CsvSummary {
  fileName: string
  rowCount: number | null
  error: string | null
}

function readCsvSummary(file: File, cb: (summary: CsvSummary) => void) {
  const reader = new FileReader()
  reader.onload = () => {
    const text = String(reader.result ?? "")
    const lines = text.split(/\r?\n/).filter((line) => line.trim().length > 0)
    if (lines.length === 0) {
      cb({ fileName: file.name, rowCount: null, error: "This file is empty." })
      return
    }
    const header = lines[0].toLowerCase()
    if (!header.includes("name") || !header.includes("email")) {
      cb({
        fileName: file.name,
        rowCount: null,
        error: 'The first row needs "Name" and "Email" columns.',
      })
      return
    }
    cb({ fileName: file.name, rowCount: lines.length - 1, error: null })
  }
  reader.onerror = () =>
    cb({ fileName: file.name, rowCount: null, error: "Couldn't read this file." })
  reader.readAsText(file)
}

export function CsvDropzone({ onFileAccepted }: CsvDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragActive, setDragActive] = useState(false)
  const [summary, setSummary] = useState<CsvSummary | null>(null)

  const acceptFile = useCallback(
    (file: File) => {
      // ponytail: .csv text files only — xlsx is zipped XML, parsing it needs a real lib
      if (!file.name.toLowerCase().endsWith(".csv")) {
        setSummary({
          fileName: file.name,
          rowCount: null,
          error: "Only .csv files are supported right now.",
        })
        toast.error("Unsupported file", {
          description: "Save your spreadsheet as CSV and drop it here.",
        })
        return
      }
      readCsvSummary(file, setSummary)
      onFileAccepted(file)
    },
    [onFileAccepted],
  )

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setDragActive(false)
    const file = e.dataTransfer.files?.[0]
    if (file) acceptFile(file)
  }

  function reset() {
    setSummary(null)
    if (inputRef.current) inputRef.current.value = ""
  }

  if (summary) {
    return (
      <div
        className={`flex items-center gap-3 rounded-xl border p-4 ${
          summary.error ? "border-destructive/40 bg-destructive/10" : "border-success/40 bg-success/10"
        }`}
      >
        <span
          className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${
            summary.error ? "bg-destructive/15 text-destructive" : "bg-success/15 text-success"
          }`}
        >
          {summary.error ? <TriangleAlert className="size-5" /> : <CheckCircle2 className="size-5" />}
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-foreground">{summary.fileName}</p>
          <p className="text-xs text-muted-foreground">
            {summary.error ??
              (summary.rowCount !== null
                ? `${summary.rowCount} recipients detected`
                : "Processing your spreadsheet…")}
          </p>
        </div>
        <Button type="button" variant="ghost" size="icon-sm" onClick={reset} aria-label="Remove file">
          <X />
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
          Drop your <span className="text-primary underline underline-offset-2">CSV</span> here
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          or click to browse — first row needs Name and Email columns
        </p>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept=".csv"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) acceptFile(file)
        }}
      />
    </div>
  )
}
