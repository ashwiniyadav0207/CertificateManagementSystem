"use client"

import { useState } from "react"
import { useRouter } from "next/navigation"
import Link from "next/link"
import { ShieldCheck, Search, ArrowRight, Lock, KeyRound, CheckCircle2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Logo } from "@/components/logo"
import { Footer } from "@/components/footer"
import { DitherShader } from "@/components/ui/dither-shader"

export default function VerifySearchPage() {
  const router = useRouter()
  const [certId, setCertId] = useState("")

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = certId.trim()
    if (trimmed) {
      router.push(`/verify/${encodeURIComponent(trimmed)}`)
    }
  }

  return (
    <div className="relative min-h-screen overflow-hidden bg-neutral-950 text-white dark">
      {/* Background Dither */}
      <div className="pointer-events-none absolute inset-0 z-0 opacity-20">
        <DitherShader
          src="/images/dither-bg.jpg"
          gridSize={4}
          ditherMode="bayer"
          colorMode="grayscale"
          primaryColor="#000000"
          secondaryColor="#ffffff"
          invert={false}
          animated={true}
          animationSpeed={0.008}
          threshold={0.5}
          className="h-full w-full"
        />
      </div>

      <header className="relative z-10 border-b border-border/70">
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between px-6">
          <Logo />
          <Link
            href="/login"
            className="text-xs font-medium text-muted-foreground transition-colors hover:text-foreground"
          >
            NGO Admin Console →
          </Link>
        </div>
      </header>

      <main className="relative z-10 mx-auto max-w-4xl px-6 py-20 text-center">
        <div className="inline-flex items-center gap-2 rounded-full border border-primary/30 bg-primary/10 px-4 py-1.5 text-xs font-medium text-primary mb-6">
          <ShieldCheck className="size-4" />
          Independent Cryptographic Verification
        </div>

        <h1 className="font-serif text-4xl font-semibold tracking-tight text-foreground sm:text-5xl">
          Verify Official Credentials
        </h1>
        <p className="mx-auto mt-4 max-w-xl text-base text-muted-foreground leading-relaxed">
          Every certificate issued by partner organizations is signed with an X.509 private key and independently verifiable against the Root Certificate Authority.
        </p>

        {/* Search form */}
        <form onSubmit={handleSearch} className="mx-auto mt-10 max-w-lg">
          <div className="flex flex-col sm:flex-row items-center gap-2 rounded-2xl border border-border bg-card/80 p-2 shadow-2xl backdrop-blur-md">
            <div className="relative w-full flex-1">
              <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
              <Input
                value={certId}
                onChange={(e) => setCertId(e.target.value)}
                placeholder="Enter Certificate ID (e.g. 090fab0c10ac...)"
                className="pl-10 border-0 bg-transparent text-sm focus-visible:ring-0 text-foreground"
              />
            </div>
            <Button type="submit" className="w-full sm:w-auto shrink-0">
              Verify
              <ArrowRight className="size-4 ml-1.5" />
            </Button>
          </div>
        </form>

        {/* Trust Badges */}
        <div className="mt-16 grid grid-cols-1 sm:grid-cols-3 gap-6 text-left">
          <div className="rounded-xl border border-border/60 bg-card/40 p-5 backdrop-blur-sm">
            <div className="size-8 rounded-lg bg-primary/10 flex items-center justify-center text-primary mb-3">
              <KeyRound className="size-4" />
            </div>
            <h3 className="text-sm font-semibold text-foreground">Offline Root CA</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Public Root CA certificates allow independent third-party validation without contacting centralized servers.
            </p>
          </div>

          <div className="rounded-xl border border-border/60 bg-card/40 p-5 backdrop-blur-sm">
            <div className="size-8 rounded-lg bg-primary/10 flex items-center justify-center text-primary mb-3">
              <Lock className="size-4" />
            </div>
            <h3 className="text-sm font-semibold text-foreground">Cryptographic CMS Signatures</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Detached PKCS#7 / CMS digital signatures prove document authenticity and protect against forgery.
            </p>
          </div>

          <div className="rounded-xl border border-border/60 bg-card/40 p-5 backdrop-blur-sm">
            <div className="size-8 rounded-lg bg-primary/10 flex items-center justify-center text-primary mb-3">
              <CheckCircle2 className="size-4" />
            </div>
            <h3 className="text-sm font-semibold text-foreground">Tamper-Evident SHA-256</h3>
            <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
              Any alteration to a certificate PDF changes its hash instantly, invalidating verification.
            </p>
          </div>
        </div>
      </main>

      <Footer />
    </div>
  )
}
