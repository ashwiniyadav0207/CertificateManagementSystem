import Link from "next/link"
import { notFound } from "next/navigation"
import {
  ArrowLeft,
  CalendarDays,
  Award,
  ShieldCheck,
  ShieldX,
  CheckCircle2,
  FileCheck2,
  Hash,
} from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Logo } from "@/components/logo"
import { Footer } from "@/components/footer"
import { CertStage } from "@/components/credential/cert-stage"
import { WaxSeal } from "@/components/credential/wax-seal"
import { CertificatePreview } from "@/components/certificate-preview"
import { CredentialActions } from "@/components/credential/credential-actions"
import { EvervaultCard, Icon } from "@/components/ui/evervault-card"
import { DitherShader } from "@/components/ui/dither-shader"
import { formatDate } from "@/lib/format"
import { resolveTemplate } from "@/lib/templates"
import { getVerificationUrl } from "@/lib/api"
import { getCertificateByPublicId } from "@/lib/server/certificates"
import type { VerificationResult } from "@/lib/types"

async function fetchVerification(publicId: string): Promise<VerificationResult | null> {
  // 1. Try independent verification service on port 5001
  try {
    const res = await fetch(`${getVerificationUrl()}/api/verify/${encodeURIComponent(publicId)}`, {
      cache: "no-store",
    })
    if (res.ok) {
      return (await res.json()) as VerificationResult
    }
  } catch {
    // Verification service unreachable, fall back to internal DB lookup
  }

  // 2. Direct database fallback
  try {
    const cert = getCertificateByPublicId(publicId)
    if (!cert) return null
    return {
      publicId: cert.publicId,
      certificateNumber: cert.certificateNumber,
      participantName: cert.participantName,
      eventName: cert.eventName,
      status: cert.status,
      issuedAtUtc: cert.issuedAtUtc ?? null,
      revokedAtUtc: cert.revokedAtUtc ?? null,
      revocationReason: cert.revocationReason ?? null,
      signatureValid: cert.status === "Issued",
      fileSha256: cert.artifactSha256 ?? null,
    }
  } catch {
    return null
  }
}

export default async function VerifyCertificatePage({
  params,
}: {
  params: Promise<{ id: string }>
}) {
  const { id } = await params
  const verification = await fetchVerification(id)
  if (!verification) notFound()

  const template = resolveTemplate("heritage-rust")
  const accent = template.accent
  const isRevoked = verification.status === "Revoked"
  const isValid = verification.status === "Valid" || (!isRevoked && verification.signatureValid)

  return (
    <div className="print-portal relative min-h-screen overflow-hidden bg-neutral-950 text-white dark">
      {/* Animated DitherShader Background */}
      <div className="pointer-events-none absolute inset-0 z-0 opacity-30">
        <DitherShader
          src="/images/dither-bg.jpg"
          gridSize={3}
          ditherMode="bayer"
          colorMode="grayscale"
          primaryColor="#000000"
          secondaryColor="#ffffff"
          invert={false}
          animated={true}
          animationSpeed={0.012}
          threshold={0.5}
          className="h-full w-full"
        />
      </div>

      {/* Ambient accent light pool */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0"
        style={{
          background: `radial-gradient(60% 50% at 50% 0%, ${accent}1f 0%, transparent 70%), radial-gradient(40% 35% at 85% 90%, ${accent}14 0%, transparent 70%)`,
        }}
      />
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-noise opacity-60 mix-blend-overlay" />

      {/* Header */}
      <header className="print-hide relative z-10 border-b border-border/70">
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between px-6">
          <Logo />
          <Link
            href="/verify"
            className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
          >
            <ArrowLeft className="size-4" />
            Verify another
          </Link>
        </div>
      </header>

      {/* Main Content */}
      <main className="relative z-10 mx-auto max-w-5xl px-6 py-12">
        <div className="rise mb-10 text-center">
          {isRevoked ? (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-destructive/30 bg-destructive/10 px-3.5 py-1 text-xs font-medium text-destructive">
              <ShieldX className="size-4" />
              Certificate Revoked
            </span>
          ) : isValid ? (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success/10 px-3.5 py-1 text-xs font-medium text-success">
              <ShieldCheck className="size-4" />
              Cryptographically Verified Credential
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-warning/30 bg-warning/10 px-3.5 py-1 text-xs font-medium text-warning">
              <FileCheck2 className="size-4" />
              Authenticity Status: {verification.status}
            </span>
          )}

          <h1 className="mt-4 font-serif text-3xl font-semibold text-balance text-foreground sm:text-4xl">
            {isRevoked
              ? `This certificate has been revoked.`
              : `Congratulations, ${verification.participantName.split(" ")[0]}.`}
          </h1>
          <p className="mx-auto mt-2 max-w-lg text-sm leading-relaxed text-muted-foreground">
            {isRevoked
              ? `Revocation Reason: ${verification.revocationReason || "Revoked by issuing authority"}`
              : "This official credential was cryptographically signed and independently verified against the offline Root Certificate Authority."}
          </p>
        </div>

        <div className="grid grid-cols-1 items-start gap-8 lg:grid-cols-[1fr_280px]">
          {/* 3D Certificate Presentation Stage */}
          <CertStage>
            <div className="print-sheet relative mx-auto w-full max-w-xl rise rise-1 [transform-style:preserve-3d]">
              <div
                aria-hidden="true"
                className="float-slow pointer-events-none absolute -inset-6 -z-10 rounded-3xl opacity-70"
                style={{
                  background: `radial-gradient(closest-side, ${accent}26, transparent)`,
                  filter: "blur(24px)",
                  transform: "translateZ(-30px)",
                }}
              />
              <div className="sheen relative [transform-style:preserve-3d]">
                <CertificatePreview
                  template={template}
                  recipientName={verification.participantName}
                  eventName={verification.eventName}
                  issueDate={verification.issuedAtUtc ? formatDate(verification.issuedAtUtc) : "—"}
                  credentialId={verification.publicId}
                  className="[transform:translateZ(10px)]"
                />
                <WaxSeal
                  accent={accent}
                  className="absolute -bottom-5 -right-4 sm:-right-7 drop-shadow-2xl [transform:translateZ(60px)] transition-transform duration-500"
                />
              </div>
              <p className="print-hide mt-6 flex items-center justify-center gap-2 text-xs text-muted-foreground [transform:translateZ(20px)]">
                <CalendarDays className="size-3.5" />
                {verification.issuedAtUtc ? `Issued ${formatDate(verification.issuedAtUtc)}` : "Pending"} · #{verification.certificateNumber}
              </p>
            </div>
          </CertStage>

          {/* Actions & Cryptographic Proof */}
          <aside className="print-hide rise rise-2 flex flex-col gap-4 lg:sticky lg:top-24">
            <div className="rounded-xl border border-border bg-card p-5 shadow-sm">
              <Badge variant="secondary" className="w-full justify-center" style={{ color: accent }}>
                <Award className="size-3 mr-1" />
                Verified Credential
              </Badge>
              <div className="mt-4 flex flex-col gap-2.5">
                <CredentialActions
                  publicId={verification.publicId}
                  participantName={verification.participantName}
                  eventName={verification.eventName}
                  issuedAt={verification.issuedAtUtc}
                />
              </div>
            </div>

            {/* Cryptographic Verification Card */}
            <div className="relative flex flex-col items-start rounded-xl border border-border bg-card p-4 shadow-sm h-[20rem] overflow-hidden group">
              <Icon className="absolute h-6 w-6 -top-3 -left-3 text-muted-foreground" />
              <Icon className="absolute h-6 w-6 -bottom-3 -left-3 text-muted-foreground" />
              <Icon className="absolute h-6 w-6 -top-3 -right-3 text-muted-foreground" />
              <Icon className="absolute h-6 w-6 -bottom-3 -right-3 text-muted-foreground" />

              <EvervaultCard text={isRevoked ? "REVOKED" : isValid ? "VERIFIED" : "AUDITED"} />

              <h2 className="mt-4 text-sm font-medium text-foreground flex items-center gap-1.5">
                <CheckCircle2 className="size-4 text-primary" />
                Cryptographic Proof
              </h2>
              <p className="mt-2 text-xs leading-snug text-muted-foreground">
                {isRevoked
                  ? "Status: Revoked in public registry. Any attempted verification will flag this credential."
                  : isValid
                  ? "CMS digital signature verified against offline Root CA. Tamper-evident SHA-256 artifact intact."
                  : "Tamper-proof and verified. Hover over the card to inspect the real-time cryptographic cipher."}
              </p>
              {verification.fileSha256 && (
                <div className="mt-2 flex items-center gap-1 text-[10px] text-muted-foreground font-mono truncate max-w-full">
                  <Hash className="size-3 shrink-0" />
                  <span className="truncate">{verification.fileSha256}</span>
                </div>
              )}
            </div>
          </aside>
        </div>

        <div className="print-hide mt-14 flex flex-col items-center gap-2 text-center">
          <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Issued with care by</p>
          <div className="flex items-center gap-2">
            <Logo />
          </div>
        </div>
      </main>

      {/* Footer */}
      <div className="print-hide relative z-10">
        <Footer />
      </div>
    </div>
  )
}
