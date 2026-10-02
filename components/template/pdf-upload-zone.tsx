"use client"

import { useRef, useState, useCallback } from "react"
import { Upload, FileUp, AlertCircle, CheckCircle2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"

interface PdfUploadZoneProps {
  onFileAccepted: (file: File) => void
  disabled?: boolean
}

interface UploadStatus {
  fileName: string | null
  error: string | null
  success: boolean
  progress: number
}

export function PdfUploadZone({ onFileAccepted, disabled }: PdfUploadZoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragActive, setDragActive] = useState(false)
  const [status, setStatus] = useState<UploadStatus>({
    fileName: null,
    error: null,
    success: false,
    progress: 0,
  })

  const validateAndAcceptFile = useCallback(
    (file: File) => {
      // Check file extension
      if (!file.name.toLowerCase().endsWith(".pdf")) {
        setStatus({
          fileName: file.name,
          error: "Only PDF files are supported.",
          success: false,
          progress: 0,
        })
        toast.error("Invalid file type", {
          description: "Please upload a PDF file.",
        })
        return
      }

      // Check file size (max 10MB)
      const maxSize = 10 * 1024 * 1024 // 10MB
      if (file.size > maxSize) {
        setStatus({
          fileName: file.name,
          error: `File too large. Maximum size is 10MB (your file: ${(file.size / 1024 / 1024).toFixed(2)}MB).`,
          success: false,
          progress: 0,
        })
        toast.error("File too large", {
          description: "Maximum file size is 10MB.",
        })
        return
      }

      // Validate PDF by checking magic bytes
      const reader = new FileReader()
      reader.onload = (e) => {
        const arr = new Uint8Array(e.target?.result as ArrayBuffer).subarray(0, 4)
        const header = arr.reduce((str, byte) => str + String.fromCharCode(byte), "")
        const isPdf = header === "%PDF"

        if (!isPdf) {
          setStatus({
            fileName: file.name,
            error: "This file is not a valid PDF.",
            success: false,
            progress: 0,
          })
          toast.error("Invalid PDF", {
            description: "The file doesn't appear to be a valid PDF.",
          })
          return
        }

        // File is valid
        setStatus({
          fileName: file.name,
          error: null,
          success: true,
          progress: 100,
        })
        toast.success("PDF uploaded", {
          description: `${file.name} is ready to use.`,
        })
        onFileAccepted(file)
      }

      reader.readAsArrayBuffer(file.slice(0, 4))
    },
    [onFileAccepted]
  )

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    e.stopPropagation()
    setDragActive(false)

    const file = e.dataTransfer.files?.[0]
    if (file) {
      validateAndAcceptFile(file)
    }
  }

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) {
      validateAndAcceptFile(file)
    }
  }

  const reset = () => {
    setStatus({
      fileName: null,
      error: null,
      success: false,
      progress: 0,
    })
    if (inputRef.current) {
      inputRef.current.value = ""
    }
  }

  if (status.fileName) {
    return (
      <div
        className={`flex items-center gap-3 rounded-xl border p-4 ${
          status.error ? "border-destructive/40 bg-destructive/10" : "border-success/40 bg-success/10"
        }`}
      >
        <span
          className={`flex size-10 shrink-0 items-center justify-center rounded-lg ${
            status.error ? "bg-destructive/15 text-destructive" : "bg-success/15 text-success"
          }`}
        >
          {status.error ? (
            <AlertCircle className="size-5" />
          ) : (
            <CheckCircle2 className="size-5" />
          )}
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-foreground">{status.fileName}</p>
          <p className="text-xs text-muted-foreground">
            {status.error || `File size: ${(status.progress / 100 * 100).toFixed(0)}%`}
          </p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          onClick={reset}
          aria-label="Remove file"
        >
          ✕
        </Button>
      </div>
    )
  }

  return (
    <div
      onDragOver={(e) => {
        e.preventDefault()
        e.stopPropagation()
        setDragActive(true)
      }}
      onDragLeave={() => setDragActive(false)}
      onDrop={handleDrop}
      onClick={() => !disabled && inputRef.current?.click()}
      onKeyDown={(e) => {
        if (!disabled && (e.key === "Enter" || e.key === " ")) {
          e.preventDefault()
          inputRef.current?.click()
        }
      }}
      role="button"
      tabIndex={disabled ? -1 : 0}
      className={`flex cursor-pointer flex-col items-center justify-center gap-4 rounded-xl border-2 border-dashed p-10 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
        disabled
          ? "cursor-not-allowed border-border/50 bg-muted/20 opacity-50"
          : dragActive
            ? "border-primary bg-primary/5"
            : "border-border bg-muted/40 hover:border-primary/40"
      }`}
    >
      <div className="pointer-events-none flex h-16 items-center justify-center">
        <div className={dragActive ? "text-primary" : "text-muted-foreground"}>
          {dragActive ? (
            <FileUp className="size-8" />
          ) : (
            <Upload className="size-8" />
          )}
        </div>
      </div>
      <div>
        <p className="text-sm font-medium text-foreground">
          Drop your <span className="font-semibold text-primary">PDF</span> here
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          or click to browse — max 10MB, must be a valid PDF
        </p>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept=".pdf"
        disabled={disabled}
        className="hidden"
        onChange={handleInputChange}
        aria-label="Upload PDF template"
      />
    </div>
  )
}
