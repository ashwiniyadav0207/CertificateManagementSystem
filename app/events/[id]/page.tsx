import Link from "next/link"
import { notFound } from "next/navigation"
import { ArrowLeft, Send, Mail, ShieldCheck, Share2, Users } from "lucide-react"
import { LinkedinIcon } from "@/components/linkedin-icon"
import { AppShell } from "@/components/dashboard/app-shell"
import { Button } from "@/components/ui/button"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Progress } from "@/components/ui/progress"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription, EmptyContent } from "@/components/ui/empty"
import { CountUp } from "@/components/dashboard/count-up"
import { resolveTemplate } from "@/lib/templates"
import { formatDate } from "@/lib/format"
import { getEngineUrl, getApiKey } from "@/lib/api"
import type { EventDetail, CertificateSummary } from "@/lib/types"

const statusStyles: Record<string, string> = {
  Issued: "bg-success/10 text-success",
  Pending: "bg-warning/15 text-warning",
  Revoked: "bg-destructive/10 text-destructive",
  Failed: "bg-destructive/10 text-destructive",
}

async function fetchEventDetail(eventId: string): Promise<EventDetail | null> {
  try {
    const res = await fetch(`${getEngineUrl()}/internal/events/${encodeURIComponent(eventId)}?page=1&pageSize=50`, {
      headers: { "X-Api-Key": getApiKey() },
      cache: "no-store",
    })
    if (!res.ok) return null
    return (await res.json()) as EventDetail
  } catch {
    return null
  }
}

function getInitials(name: string) {
  return name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .slice(0, 2)
    .toUpperCase()
}

export default async function EventDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  const detail = await fetchEventDetail(id)
  if (!detail) notFound()

  const template = resolveTemplate(detail.templateId)
  const credentialList = detail.certificates.items
  const progress = detail.totalCount > 0 ? Math.round((detail.issuedCount / detail.totalCount) * 100) : 0

  return (
    <AppShell
      action={
        <Button render={<Link href={`/events/${detail.eventId}/issue`} />}>
          <Send data-icon="inline-start" />
          Issue credentials
        </Button>
      }
    >
      <Link
        href="/events"
        className="mb-6 inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft className="size-4" />
        Back to events
      </Link>

      <div className="mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-2">
            <h1 className="font-serif text-2xl font-semibold text-foreground sm:text-3xl">{detail.eventName}</h1>
            <span
              className="rounded-full px-2 py-0.5 text-[11px] font-medium"
              style={{ backgroundColor: template.accentSoft, color: template.accent }}
            >
              {template.name}
            </span>
          </div>
        </div>
        <div className="flex min-w-48 flex-col gap-1.5">
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span className="flex items-center gap-1">
              <Users className="size-3.5" />
              <CountUp value={detail.issuedCount} /> / {detail.totalCount} issued
            </span>
            <span className="font-medium text-foreground">
              <CountUp value={progress} />%
            </span>
          </div>
          <Progress value={progress} className="h-1.5" />
        </div>
      </div>

      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-sm font-medium text-foreground">All credentials</h2>
      </div>

      {credentialList.length > 0 ? (
        <div className="overflow-hidden rounded-xl border border-border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Certificate #</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody className="stagger">
              {credentialList.map((cert: CertificateSummary) => (
                <TableRow key={cert.publicId}>
                  <TableCell>
                    <div className="flex items-center gap-2.5">
                      <Avatar className="size-7">
                        <AvatarFallback className="text-[11px]">
                          {getInitials(cert.participantName)}
                        </AvatarFallback>
                      </Avatar>
                      <span className="font-medium text-foreground">{cert.participantName}</span>
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{cert.participantEmail ?? "—"}</TableCell>
                  <TableCell className="font-mono text-xs text-muted-foreground">{cert.certificateNumber}</TableCell>
                  <TableCell>
                    <Badge className={statusStyles[cert.status] ?? ""} variant="secondary">
                      {cert.status === "Issued" && <ShieldCheck className="size-3" data-icon="inline-start" />}
                      {cert.status}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex items-center justify-end gap-1">
                      <Button variant="ghost" size="icon-sm" aria-label="Email">
                        <Mail />
                      </Button>
                      <Button variant="ghost" size="icon-sm" aria-label="Share">
                        <Share2 />
                      </Button>
                      <Button variant="ghost" size="icon-sm" aria-label="LinkedIn">
                        <LinkedinIcon className="size-4" />
                      </Button>
                      <Button variant="link" size="sm" render={<Link href={`/credentials/${cert.publicId}`} />}>
                        View
                      </Button>
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
              <Send />
            </EmptyMedia>
            <EmptyTitle>No credentials issued yet</EmptyTitle>
            <EmptyDescription>Issue your first batch of credentials for this event.</EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Button render={<Link href={`/events/${detail.eventId}/issue`} />}>
              <Send data-icon="inline-start" />
              Issue credentials
            </Button>
          </EmptyContent>
        </Empty>
      )}
    </AppShell>
  )
}
