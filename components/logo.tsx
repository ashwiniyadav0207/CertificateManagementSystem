import { cn } from "@/lib/utils"
import Image from "next/image"

export function LogoMark({ className }: { className?: string }) {
  return (
    <div className={cn("relative size-8", className)}>
      <Image
        src="/images/credentia-logo.svg"
        alt="Credentia Logo"
        fill
        className="object-contain"
        priority
      />
    </div>
  )
}

export function Logo({ className, wordmarkClassName }: { className?: string; wordmarkClassName?: string }) {
  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      <LogoMark />
      <span className={cn("font-serif text-lg font-semibold tracking-tight text-foreground", wordmarkClassName)}>
        Credentia
      </span>
    </div>
  )
}
