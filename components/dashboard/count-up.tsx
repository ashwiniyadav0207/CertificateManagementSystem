"use client"

import { useEffect, useRef, useState } from "react"

export function CountUp({
  value,
  duration = 900,
  className,
}: {
  value: number
  duration?: number
  className?: string
}) {
  // Start at the real value so SSR and no-JS users see the truth.
  const [display, setDisplay] = useState(value)
  const played = useRef(false)

  useEffect(() => {
    if (played.current) return
    played.current = true
    if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) return
    let raf = 0
    const start = performance.now()
    const tick = (now: number) => {
      const t = Math.min((now - start) / duration, 1)
      const eased = 1 - Math.pow(1 - t, 3)
      setDisplay(Math.round(value * eased))
      if (t < 1) raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [value, duration])

  return <span className={`tabnum ${className ?? ""}`}>{display}</span>
}
