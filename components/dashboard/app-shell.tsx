import Link from "next/link"
import { Logo } from "@/components/logo"
import { UserMenu } from "@/components/dashboard/user-menu"

export function AppShell({
  children,
  action,
}: {
  children: React.ReactNode
  action?: React.ReactNode
}) {
  return (
    <div className="min-h-screen bg-background">
      <div className="pointer-events-none fixed inset-0 -z-10 bg-noise opacity-40" />
      <header className="sticky top-0 z-40 border-b border-border/70 bg-background/85 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
          <Link href="/events">
            <Logo />
          </Link>
          <div className="flex items-center gap-3">
            {action}
            <UserMenu />
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-6 py-10">
        <div className="page-enter">{children}</div>
      </main>
    </div>
  )
}
