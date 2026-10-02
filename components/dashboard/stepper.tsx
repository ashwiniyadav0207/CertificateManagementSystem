import { Check } from "lucide-react"
import { cn } from "@/lib/utils"

export function Stepper({ steps, current }: { steps: string[]; current: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-2.5" aria-label="Progress">
      {steps.map((label, i) => {
        const n = i + 1
        const done = n < current
        const active = n === current
        return (
          <li
            key={label}
            className="flex items-center gap-2.5"
            aria-current={active ? "step" : undefined}
          >
            <span
              className={cn(
                "flex size-6 items-center justify-center rounded-full border text-xs font-medium",
                done || active
                  ? "border-primary bg-primary text-primary-foreground"
                  : "border-border text-muted-foreground"
              )}
            >
              {done ? <Check className="size-3.5" /> : n}
            </span>
            <span
              className={cn(
                "text-xs font-medium",
                done || active ? "text-foreground" : "text-muted-foreground"
              )}
            >
              {label}
            </span>
            {n < steps.length && (
              <span
                aria-hidden="true"
                className={cn(
                  "h-px w-8 origin-left",
                  done ? "grow-x bg-primary" : "bg-border"
                )}
              />
            )}
          </li>
        )
      })}
    </ol>
  )
}
