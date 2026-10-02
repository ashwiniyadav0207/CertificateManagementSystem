"use client"

export function AuthBackground() {
  return (
    <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden bg-background">
      <div className="absolute -top-32 -left-32 size-[30rem] rounded-full bg-primary/10 blur-[100px]" />
      <div className="absolute -bottom-32 -right-32 size-[30rem] rounded-full bg-amber-500/10 blur-[100px]" />
      <div className="absolute inset-0 bg-noise opacity-25" />
    </div>
  )
}
