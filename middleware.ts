import { NextResponse } from "next/server"
import type { NextRequest } from "next/server"

export function middleware(request: NextRequest) {
  const pathname = request.nextUrl.pathname
  const verificationBase = process.env.VERIFICATION_URL || "http://localhost:5001"

  // 1. Direct any verification attempts to the single official verification portal
  if (pathname.startsWith("/credentials/") || pathname.startsWith("/verify/")) {
    const id = pathname.replace(/^\/(credentials|verify)\//, "")
    if (id) {
      return NextResponse.redirect(`${verificationBase}/verify/${encodeURIComponent(id)}`, 307)
    }
    return NextResponse.redirect(verificationBase, 307)
  }

  // 2. Allow static assets and public APIs
  if (
    pathname.startsWith("/_next/") ||
    pathname.startsWith("/api/auth/") ||
    pathname === "/api/health" ||
    pathname.includes(".")
  ) {
    return NextResponse.next()
  }

  // 3. Check for active admin session cookie
  const session = request.cookies.get("credentia-session")
  const hasSession = Boolean(session?.value)

  // 4. Handle login page
  if (pathname === "/login") {
    if (hasSession) {
      return NextResponse.redirect(new URL("/events", request.url))
    }
    return NextResponse.next()
  }

  // 5. Require authentication for all admin routes (including root /)
  if (!hasSession) {
    const loginUrl = new URL("/login", request.url)
    return NextResponse.redirect(loginUrl)
  }

  // 6. Redirect authenticated root / to /events
  if (pathname === "/") {
    return NextResponse.redirect(new URL("/events", request.url))
  }

  return NextResponse.next()
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
}
