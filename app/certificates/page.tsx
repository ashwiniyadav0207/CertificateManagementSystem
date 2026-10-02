"use client"

import { useEffect, useState, useMemo } from "react"
import Link from "next/link"
import { 
  Award, 
  Search, 
  Download, 
  ExternalLink, 
  ShieldCheck, 
  ShieldX, 
  AlertTriangle, 
  X, 
  Loader2, 
  Filter,
  RefreshCw
} from "lucide-react"
import { toast } from "sonner"
import { AppShell } from "@/components/dashboard/app-shell"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Input } from "@/components/ui/input"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from "@/components/ui/empty"
import { formatDate } from "@/lib/format"
import type { CertificateSummary } from "@/lib/types"

const statusStyles: Record<string, string> = {
  Issued: "bg-success/10 text-success border-success/20",
  Pending: "bg-warning/15 text-warning border-warning/20",
  Revoked: "bg-destructive/10 text-destructive border-destructive/20",
  Failed: "bg-destructive/10 text-destructive border-destructive/20",
}

function getInitials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .slice(0, 2)
    .toUpperCase()
}

export default function CertificatesRegistryPage() {
  const [certificates, setCertificates] = useState<CertificateSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState("")
  const [statusFilter, setStatusFilter] = useState<"All" | "Issued" | "Revoked">("All")
  
  // Revocation modal state
  const [revokingCert, setRevokingCert] = useState<CertificateSummary | null>(null)
  const [revocationReason, setRevocationReason] = useState("")
  const [isSubmittingRevocation, setIsSubmittingRevocation] = useState(false)

  const verificationBase = process.env.NEXT_PUBLIC_VERIFICATION_URL || "http://localhost:5001"

  async function loadCertificates() {
    setLoading(true)
    try {
      const res = await fetch("/api/certificates?page=1&pageSize=100")
      if (!res.ok) throw new Error("Failed to load certificates")
      const data = await res.json()
      setCertificates(data.items || [])
    } catch (err: any) {
      toast.error("Could not load certificates", { description: err.message })
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadCertificates()
  }, [])

  const filteredCertificates = useMemo(() => {
    return certificates.filter((cert) => {
      const matchesStatus = statusFilter === "All" || cert.status === statusFilter
      const q = search.toLowerCase().trim()
      if (!q) return matchesStatus

      const matchesSearch =
        cert.participantName.toLowerCase().includes(q) ||
        cert.certificateNumber.toLowerCase().includes(q) ||
        (cert.participantEmail && cert.participantEmail.toLowerCase().includes(q)) ||
        cert.eventName.toLowerCase().includes(q)

      return matchesStatus && matchesSearch
    })
  }, [certificates, search, statusFilter])

  async function handleConfirmRevocation() {
    if (!revokingCert) return
    if (!revocationReason.trim()) {
      toast.error("Reason required", { description: "Please provide a reason for certificate revocation." })
      return
    }

    setIsSubmittingRevocation(true)
    try {
      const res = await fetch(`/api/certificates/${revokingCert.publicId}/revoke`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason: revocationReason.trim() }),
      })

      if (!res.ok) {
        const errorText = await res.text()
        throw new Error(errorText || "Revocation request failed")
      }

      toast.success("Certificate Revoked", {
        description: `Certificate for ${revokingCert.participantName} has been revoked.`,
      })

      // Update local state
      setCertificates((prev) =>
        prev.map((c) =>
          c.publicId === revokingCert.publicId
            ? { ...c, status: "Revoked", revokedAt: new Date().toISOString(), revocationReason: revocationReason.trim() }
            : c
        )
      )
      setRevokingCert(null)
      setRevocationReason("")
    } catch (err: any) {
      toast.error("Revocation failed", { description: err.message })
    } finally {
      setIsSubmittingRevocation(false)
    }
  }

  return (
    <AppShell
      action={
        <Button variant="outline" size="sm" onClick={loadCertificates} disabled={loading}>
          <RefreshCw className={`size-3.5 ${loading ? "animate-spin" : ""}`} />
          Refresh
        </Button>
      }
    >
      <div className="mb-8 flex flex-col gap-1.5">
        <h1 className="font-serif text-3xl font-semibold text-foreground">Certificate Registry</h1>
        <p className="text-sm text-muted-foreground">
          Complete audit trail of all credentials issued across your cohorts. Directly download signed PDFs, audit verification, or manage revocations.
        </p>
      </div>

      {/* Filters & Search Toolbar */}
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative w-full max-w-sm">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by recipient, cert #, email..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>

        <div className="flex items-center gap-2">
          <Filter className="size-3.5 text-muted-foreground" />
          <span className="text-xs text-muted-foreground">Status:</span>
          {(["All", "Issued", "Revoked"] as const).map((filter) => (
            <button
              key={filter}
              type="button"
              onClick={() => setStatusFilter(filter)}
              className={`rounded-lg px-2.5 py-1 text-xs font-medium transition-colors ${
                statusFilter === filter
                  ? "bg-primary text-primary-foreground shadow-sm"
                  : "bg-muted text-muted-foreground hover:bg-muted/80 hover:text-foreground"
              }`}
            >
              {filter}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="flex h-48 items-center justify-center">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : filteredCertificates.length > 0 ? (
        <div className="overflow-hidden rounded-xl border border-border bg-card shadow-sm">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Recipient</TableHead>
                <TableHead>Event / Cohort</TableHead>
                <TableHead>Certificate #</TableHead>
                <TableHead>Issued</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredCertificates.map((cert) => (
                <TableRow key={cert.publicId}>
                  <TableCell>
                    <div className="flex items-center gap-2.5">
                      <Avatar className="size-7">
                        <AvatarFallback className="text-[11px] font-medium">
                          {getInitials(cert.participantName)}
                        </AvatarFallback>
                      </Avatar>
                      <div>
                        <p className="font-medium text-foreground">{cert.participantName}</p>
                        <p className="text-xs text-muted-foreground">{cert.participantEmail || "—"}</p>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>
                    <span className="text-sm font-medium text-foreground">{cert.eventName}</span>
                  </TableCell>
                  <TableCell>
                    <span className="font-mono text-xs text-muted-foreground">{cert.certificateNumber}</span>
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {cert.issuedAt ? formatDate(cert.issuedAt) : "—"}
                  </TableCell>
                  <TableCell>
                    <Badge className={statusStyles[cert.status] ?? ""} variant="secondary">
                      {cert.status === "Issued" && <ShieldCheck className="size-3" data-icon="inline-start" />}
                      {cert.status === "Revoked" && <ShieldX className="size-3" data-icon="inline-start" />}
                      {cert.status}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex items-center justify-end gap-1.5">
                      {/* Download PDF */}
                      <a
                        href={`/api/certificates/${cert.publicId}/download`}
                        download
                        className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                        title="Download cryptographically signed PDF"
                      >
                        <Download className="size-3.5" />
                        <span className="hidden lg:inline">PDF</span>
                      </a>

                      {/* Verify on independent portal */}
                      <a
                        href={`${verificationBase}/verify/${cert.publicId}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                        title="Open in independent verification portal"
                      >
                        <ExternalLink className="size-3.5" />
                        <span className="hidden lg:inline">Verify</span>
                      </a>

                      {/* Revoke button */}
                      {cert.status !== "Revoked" ? (
                        <button
                          type="button"
                          onClick={() => {
                            setRevokingCert(cert)
                            setRevocationReason("")
                          }}
                          className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-destructive/80 transition-colors hover:bg-destructive/10 hover:text-destructive"
                          title="Revoke certificate"
                        >
                          <ShieldX className="size-3.5" />
                          <span className="hidden lg:inline">Revoke</span>
                        </button>
                      ) : (
                        <span
                          className="text-[11px] italic text-muted-foreground"
                          title={cert.revocationReason || "Revoked"}
                        >
                          Revoked
                        </span>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <Empty>
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Award />
            </EmptyMedia>
            <EmptyTitle>No certificates found</EmptyTitle>
            <EmptyDescription>
              {search || statusFilter !== "All"
                ? "No credentials match your search criteria."
                : "No certificates have been issued yet. Create an event to begin."}
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}

      {/* Revocation Confirmation Dialog */}
      {revokingCert && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 shadow-2xl">
            <div className="flex items-start justify-between gap-3">
              <div className="flex size-10 items-center justify-center rounded-full bg-destructive/10 text-destructive">
                <AlertTriangle className="size-5" />
              </div>
              <button
                type="button"
                onClick={() => setRevokingCert(null)}
                className="text-muted-foreground hover:text-foreground"
              >
                <X className="size-4" />
              </button>
            </div>

            <h3 className="mt-4 font-serif text-xl font-semibold text-foreground">
              Revoke Certificate?
            </h3>
            <p className="mt-1.5 text-sm text-muted-foreground">
              You are revoking the credential for <strong className="text-foreground">{revokingCert.participantName}</strong> ({revokingCert.certificateNumber}). The public verification portal will immediately flag this certificate as revoked.
            </p>

            <div className="mt-4">
              <label htmlFor="revocation-reason" className="block text-xs font-medium text-foreground mb-1.5">
                Revocation Reason (Required for audit trail)
              </label>
              <textarea
                id="revocation-reason"
                rows={3}
                placeholder="e.g. Issued in error, failed requirements, or student requested replacement"
                value={revocationReason}
                onChange={(e) => setRevocationReason(e.target.value)}
                className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                autoFocus
              />
            </div>

            <div className="mt-6 flex items-center justify-end gap-3">
              <Button variant="outline" onClick={() => setRevokingCert(null)} disabled={isSubmittingRevocation}>
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={handleConfirmRevocation}
                disabled={isSubmittingRevocation || !revocationReason.trim()}
              >
                {isSubmittingRevocation ? (
                  <>
                    <Loader2 className="size-4 animate-spin" />
                    Revoking…
                  </>
                ) : (
                  "Confirm Revocation"
                )}
              </Button>
            </div>
          </div>
        </div>
      )}
    </AppShell>
  )
}
