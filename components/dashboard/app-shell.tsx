import Link from "next/link"
import { Logo } from "@/components/logo"
import { UserMenu } from "@/components/dashboard/user-menu"
import { Footer } from "@/components/footer"
import { CalendarDays, Award, ExternalLink } from "lucide-react"

export function AppShell({
  children,
  action,
}: {
  children: React.ReactNode
  action?: React.ReactNode
}) {
  const verificationUrl = process.env.NEXT_PUBLIC_VERIFICATION_URL || "http://localhost:5001"

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <div className="pointer-events-none fixed inset-0 -z-10 bg-noise opacity-30" />
      
      <header className="sticky top-0 z-40 border-b border-border/70 bg-background/90 backdrop-blur-md">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
          <div className="flex items-center gap-8">
            <Link href="/events">
              <Logo />
            </Link>

            <nav className="hidden items-center gap-1 sm:flex">
              <Link
                href="/events"
                className="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              >
                <CalendarDays className="size-4" />
                Cohorts & Events
              </Link>
              <Link
                href="/certificates"
                className="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              >
                <Award className="size-4" />
                Certificate Registry
              </Link>
            </nav>
          </div>

          <div className="flex items-center gap-3">
            <a
              href={verificationUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="hidden items-center gap-1.5 rounded-lg border border-border/80 px-3 py-1.5 text-xs font-medium text-muted-foreground transition-colors hover:border-primary/50 hover:bg-muted/50 hover:text-foreground md:inline-flex"
            >
              <span>Verification Portal</span>
              <ExternalLink className="size-3.5" />
            </a>

            {action}
            <UserMenu />
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-6 py-8">
        <div className="page-enter">{children}</div>
      </main>

      <Footer />
    </div>
  )
}
