import { cn } from "@/lib/utils"
import { ShieldCheck } from "lucide-react"

export function LogoMark({ className }: { className?: string }) {
  return (
    <div className={cn("flex size-9 items-center justify-center rounded-lg bg-primary/10 border border-primary/20 text-primary shadow-sm", className)}>
      <ShieldCheck className="size-5 text-primary" />
    </div>
  )
}

export function Logo({ className, wordmarkClassName }: { className?: string; wordmarkClassName?: string }) {
  return (
    <div className={cn("flex items-center gap-3", className)}>
      <LogoMark />
      <div className="flex flex-col">
        <span className={cn("font-serif text-lg font-semibold tracking-tight text-foreground leading-none", wordmarkClassName)}>
          CertOps Portal
        </span>
        <span className="text-[10px] uppercase tracking-wider text-muted-foreground font-semibold mt-0.5">
          NGO Operations
        </span>
      </div>
    </div>
  )
}
