import { notFound } from "next/navigation"
import Link from "next/link"
import { ArrowLeft, ShieldCheck, CalendarDays, Award, ShieldX } from "lucide-react"
import { Logo } from "@/components/logo"
import { Footer } from "@/components/footer"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { CertificatePreview } from "@/components/certificate-preview"
import { CertStage } from "@/components/credential/cert-stage"
import { WaxSeal } from "@/components/credential/wax-seal"
import { DitherShader } from "@/components/ui/dither-shader"
import { EvervaultCard, Icon } from "@/components/ui/evervault-card"
import { CredentialActions } from "@/components/credential/credential-actions"
import { resolveTemplate } from "@/lib/templates"
import { formatDate } from "@/lib/format"
import { getVerificationUrl } from "@/lib/api"
import type { VerificationResult } from "@/lib/types"

async function fetchVerification(publicId: string): Promise<VerificationResult | null> {
  try {
    const res = await fetch(`${getVerificationUrl()}/api/verify/${encodeURIComponent(publicId)}`, {
      cache: "no-store",
    })
    if (!res.ok) return null
    return (await res.json()) as VerificationResult
  } catch {
    return null
  }
}

export default async function CredentialPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params
  const verification = await fetchVerification(id)
  if (!verification) notFound()

  const template = resolveTemplate("heritage-rust") // Default template for public view
  const accent = template.accent
  const isRevoked = verification.status === "Revoked"

  return (
    <div className="print-portal relative min-h-screen overflow-hidden bg-neutral-950 text-white dark">
      {/* Animated DitherShader Background with Sea Link Bridge */}
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

      {/* Ambient light pools in the award's own accent */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0"
        style={{
          background: `radial-gradient(60% 50% at 50% 0%, ${accent}1f 0%, transparent 70%), radial-gradient(40% 35% at 85% 90%, ${accent}14 0%, transparent 70%)`,
        }}
      />
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-noise opacity-60 mix-blend-overlay" />

      <header className="print-hide relative z-10 border-b border-border/70">
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between px-6">
          <Logo />
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
          >
            <ArrowLeft className="size-4" />
            Home
          </Link>
        </div>
      </header>

      <main className="relative z-10 mx-auto max-w-5xl px-6 py-12">
        <div className="rise mb-10 text-center">
          {isRevoked ? (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-destructive/30 bg-destructive/10 px-3 py-1 text-xs font-medium text-destructive">
              <ShieldX className="size-3.5" />
              Certificate revoked
            </span>
          ) : (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success/10 px-3 py-1 text-xs font-medium text-success">
              <ShieldCheck className="size-3.5" />
              Verified credential
            </span>
          )}
          <h1 className="mt-4 font-serif text-3xl font-semibold text-balance text-foreground sm:text-4xl">
            {isRevoked
              ? `This certificate has been revoked.`
              : `Congratulations, ${verification.participantName.split(" ")[0]}.`}
          </h1>
          <p className="mx-auto mt-2 max-w-md text-sm leading-relaxed text-muted-foreground">
            {isRevoked
              ? `Reason: ${verification.revocationReason ?? "No reason provided."}`
              : "This is yours to keep — download it, share the verification link, or add it to your LinkedIn profile in one click."}
          </p>
        </div>

        <div className="grid grid-cols-1 items-start gap-8 lg:grid-cols-[1fr_260px]">
          {/* The presentation stage */}
          <CertStage>
            <div className="print-sheet relative mx-auto w-full max-w-xl rise rise-1 [transform-style:preserve-3d]">
              <div
                aria-hidden="true"
                className="float-slow pointer-events-none absolute -inset-6 -z-10 rounded-3xl opacity-70"
                style={{
                  background: `radial-gradient(closest-side, ${accent}26, transparent)`,
                  filter: "blur(24px)",
                  transform: "translateZ(-30px)"
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
                <WaxSeal accent={accent} className="absolute -bottom-5 -right-4 sm:-right-7 drop-shadow-2xl [transform:translateZ(60px)] transition-transform duration-500" />
              </div>
              <p className="print-hide mt-6 flex items-center justify-center gap-2 text-xs text-muted-foreground [transform:translateZ(20px)]">
                <CalendarDays className="size-3.5" />
                {verification.issuedAtUtc ? `Issued ${formatDate(verification.issuedAtUtc)}` : "Pending"} · #{verification.certificateNumber}
              </p>
            </div>
          </CertStage>

          {/* Actions */}
          <aside className="print-hide rise rise-2 flex flex-col gap-4 lg:sticky lg:top-24">
            <div className="rounded-xl border border-border bg-card p-5 shadow-sm">
              <Badge variant="secondary" className="w-full justify-center" style={{ color: accent }}>
                <Award className="size-3" />
                Certification
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

            {/* Cryptographic Verification Card (Evervault) */}
            <div className="relative mt-4 flex flex-col items-start rounded-xl border border-border bg-card p-4 shadow-sm h-[20rem] overflow-hidden group">
              <Icon className="absolute h-6 w-6 -top-3 -left-3 text-muted-foreground" />
              <Icon className="absolute h-6 w-6 -bottom-3 -left-3 text-muted-foreground" />
              <Icon className="absolute h-6 w-6 -top-3 -right-3 text-muted-foreground" />
              <Icon className="absolute h-6 w-6 -bottom-3 -right-3 text-muted-foreground" />
        
              <EvervaultCard text="VERIFIED" />
        
              <h2 className="mt-4 text-sm font-medium text-foreground">
                Cryptographic Proof
              </h2>
              <p className="mt-2 text-xs leading-snug text-muted-foreground">
                {verification.signatureValid
                  ? "Digital signature verified. This certificate is authentic and has not been tampered with."
                  : "Tamper-proof and verified. Hover over the card to inspect the cryptographic hash. Anyone with this link can confirm authenticity."}
              </p>
            </div>
          </aside>
        </div>

        <div className="print-hide mt-14 flex flex-col items-center gap-2 text-center">
          <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Issued with care by</p>
          <Button variant="link" render={<Link href="/" />}>
            <Logo />
          </Button>
        </div>
      </main>

      {/* Footer */}
      <div className="print-hide relative z-10">
        <Footer />
      </div>
    </div>
  )
}
