import Image from "next/image"
import { ShieldCheck } from "lucide-react"
import { cn } from "@/lib/utils"
import type { CredentialTemplate } from "@/lib/templates"

interface CertificatePreviewProps {
  template: CredentialTemplate
  recipientName: string
  eventName: string
  issueDate: string
  credentialId: string
  className?: string
}

export function CertificatePreview({
  template,
  recipientName,
  eventName,
  issueDate,
  credentialId,
  className,
}: CertificatePreviewProps) {
  return (
    <div
      className={cn("relative w-full overflow-hidden rounded-md p-2.5 sm:p-3", className)}
      style={{ backgroundColor: template.accentSoft }}
    >
      <div
        className="relative flex aspect-[7/5] w-full flex-col items-center rounded-[2px] border-2 bg-[#fdf9f1] px-6 py-7 text-center sm:px-10 sm:py-9"
        style={{ borderColor: template.accent }}
      >
        <div
          className="pointer-events-none absolute inset-2 rounded-[1px] border"
          style={{ borderColor: template.accent, opacity: 0.35 }}
        />

        <div
          className="flex size-11 items-center justify-center rounded-full border sm:size-12"
          style={{ borderColor: template.accent, color: template.accent }}
        >
          <ShieldCheck className="size-5 sm:size-6" strokeWidth={1.75} />
        </div>

        <p className="mt-3 text-[10px] font-medium uppercase tracking-[0.2em] text-muted-foreground sm:text-xs">
          Certificate of Completion
        </p>
        <h3 className="mt-1 font-serif text-xl font-semibold text-balance text-foreground sm:text-3xl">
          {eventName}
        </h3>

        <p className="mt-4 text-[10px] uppercase tracking-[0.2em] text-muted-foreground sm:mt-6 sm:text-xs">
          Presented to
        </p>
        <p className="mt-1 font-serif text-2xl font-medium text-foreground sm:text-4xl">{recipientName}</p>

        <p className="mx-auto mt-3 max-w-md text-[11px] leading-relaxed text-muted-foreground sm:mt-4 sm:text-sm">
          In recognition of successful participation and demonstrated commitment throughout the program.
        </p>

        <div className="mt-5 flex w-full items-end justify-center gap-10 sm:mt-8 sm:gap-16">
          <SignatureBlock src="/certificate/signature-1.png" name="Dr. Evgeny Dengub" role="Program Director" />
          <SignatureBlock src="/certificate/signature-2.png" name="Diego Fernandez" role="Head of Department" />
        </div>

        <div className="mt-6 flex w-full items-center justify-between gap-3 border-t pt-3 sm:mt-8 sm:pt-4" style={{ borderColor: template.accent, opacity: 1 }}>
          <div className="flex items-center gap-2 text-left">
            <div className="relative size-8 shrink-0 opacity-80 sm:size-9">
              <Image src="/certificate/qr-code.png" alt="" fill className="object-contain" />
            </div>
            <p className="text-[8px] leading-tight text-muted-foreground sm:text-[10px]">
              Scan to verify this
              <br />
              certificate&apos;s authenticity
            </p>
          </div>
          <p className="text-[9px] text-muted-foreground sm:text-xs">{issueDate}</p>
        </div>
      </div>

      <div className="mt-2.5 flex items-center justify-between px-1 text-[10px] text-muted-foreground sm:text-xs">
        <span className="flex items-center gap-1.5">
          <ShieldCheck className="size-3.5 text-success" />
          <span className="font-mono">{credentialId}</span>
        </span>
        <span className="flex items-center gap-1 font-medium text-foreground">
          <ShieldCheck className="size-3.5" style={{ color: template.accent }} />
          Credentia
        </span>
      </div>
    </div>
  )
}

function SignatureBlock({ src, name, role }: { src: string; name: string; role: string }) {
  return (
    <div className="flex flex-col items-center gap-1">
      <div className="relative h-6 w-20 sm:h-8 sm:w-28">
        <Image src={src} alt="" fill className="object-contain" />
      </div>
      <div className="w-full border-t border-foreground/30 pt-1">
        <p className="text-[9px] font-medium text-foreground sm:text-xs">{name}</p>
        <p className="text-[8px] text-muted-foreground sm:text-[10px]">{role}</p>
      </div>
    </div>
  )
}
