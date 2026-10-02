import { NextResponse } from "next/server"
import type { NextRequest } from "next/server"

export function middleware(request: NextRequest) {
  // Public routes that don't need auth
  const publicPaths = ["/", "/login", "/signup", "/forgot-password", "/credentials"]
  const pathname = request.nextUrl.pathname

  // Allow public paths, API routes, static files
  if (
    publicPaths.some((p) => pathname === p || pathname.startsWith(p + "/")) ||
    pathname.startsWith("/api/") ||
    pathname.startsWith("/_next/") ||
    pathname.includes(".")
  ) {
    return NextResponse.next()
  }

  // Check for session cookie (set by login page)
  const session = request.cookies.get("credentia-session")
  if (!session?.value) {
    return NextResponse.redirect(new URL("/login", request.url))
  }

  return NextResponse.next()
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
}
