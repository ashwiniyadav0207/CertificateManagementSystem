import Link from "next/link"
import { ArrowRight } from "lucide-react"
import { buttonVariants } from "@/components/ui/button"
import { Logo } from "@/components/logo"
import { Footer } from "@/components/footer"
import PrismaticBurst from "@/components/effects/prismatic-burst"

export default function LandingPage() {
  return (
    <main className="relative min-h-screen overflow-hidden bg-background">
      <div className="pointer-events-none absolute inset-0 z-0">
        <PrismaticBurst
          animationType="rotate3d"
          intensity={2.4}
          speed={0.35}
          distort={0.6}
          rayCount={10}
          colors={["#FFD700", "#FFA500", "#F5A623", "#d99a3d"]}
          className="opacity-80"
        />
      </div>

      <div className="relative z-10 flex min-h-screen flex-col">
        <header className="flex items-center justify-between px-6 py-6 sm:px-10">
          <Logo />
          <nav className="flex items-center gap-2">
            <Link href="/login" className={buttonVariants({ variant: "ghost" })}>
              Log in
            </Link>
            <Link href="/signup" className={buttonVariants()}>
              Get Started
            </Link>
          </nav>
        </header>

        <section className="mx-auto flex max-w-5xl flex-1 flex-col items-center justify-center px-6 pb-24 text-center sm:pb-32">
          <h1 className="max-w-3xl font-serif text-5xl font-bold leading-[1.1] text-balance text-foreground sm:text-7xl">
            Certificate issuance that feels
            <span className="block text-primary">precise and modern.</span>
          </h1>

          <p className="mt-5 max-w-xl text-base leading-relaxed text-muted-foreground sm:text-lg">
            Plan a cohort, choose a template, and send verifiable credentials that are easy to trust, easy to check,
            and easy to share with the people who matter.
          </p>

          <div className="mt-8 flex flex-col items-center gap-3 sm:flex-row">
            <Link href="/signup" className={buttonVariants({ size: "lg", className: "h-11 px-6 text-base" })}>
              Start issuing
              <ArrowRight data-icon="inline-end" />
            </Link>
            <Link href="/login" className={buttonVariants({ variant: "outline", size: "lg", className: "h-11 px-6 text-base" })}>
              I already have an account
            </Link>
          </div>
        </section>

        <Footer />
      </div>
    </main>
  )
}
