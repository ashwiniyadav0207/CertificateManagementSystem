"use client"

import { useRef, useState, type ReactNode } from "react"
import { cn } from "@/lib/utils"

export function CertStage({ children, className }: { children: ReactNode; className?: string }) {
  const stageRef = useRef<HTMLDivElement>(null)
  const targetRef = useRef<HTMLDivElement>(null)
  
  const [rotate, setRotate] = useState({ x: 0, y: 0 })
  const [glare, setGlare] = useState({ x: 50, y: 50, opacity: 0 })
  const [isHovering, setIsHovering] = useState(false)

  function onPointerMove(e: React.PointerEvent) {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return
    
    const stage = stageRef.current
    if (!stage) return
    
    const rect = stage.getBoundingClientRect()
    // Calculate normalized pointer coordinates (-1 to 1)
    const x = ((e.clientX - rect.left) / rect.width) * 2 - 1
    const y = ((e.clientY - rect.top) / rect.height) * 2 - 1
    
    // Limits for rotation (max 10 degrees)
    const rotateX = y * -8
    const rotateY = x * 10

    // Glare position (percentage)
    const glareX = ((e.clientX - rect.left) / rect.width) * 100
    const glareY = ((e.clientY - rect.top) / rect.height) * 100

    setRotate({ x: rotateX, y: rotateY })
    setGlare({ x: glareX, y: glareY, opacity: 1 })
    setIsHovering(true)
  }

  function onPointerLeave() {
    setRotate({ x: 0, y: 0 })
    setGlare((prev) => ({ ...prev, opacity: 0 }))
    setIsHovering(false)
  }

  return (
    <div
      ref={stageRef}
      onPointerMove={onPointerMove}
      onPointerLeave={onPointerLeave}
      className={cn("relative z-10 w-full [perspective:1600px]", className)}
      style={{
        '--glare-x': `${glare.x}%`,
        '--glare-y': `${glare.y}%`,
        '--glare-opacity': glare.opacity,
        '--rotate-x': `${rotate.x}deg`,
        '--rotate-y': `${rotate.y}deg`,
      } as React.CSSProperties}
    >
      <div
        ref={targetRef}
        className="relative mx-auto w-full [transform-style:preserve-3d] will-change-transform"
        style={{
          transform: `rotateX(var(--rotate-x)) rotateY(var(--rotate-y)) ${isHovering ? 'scale3d(1.02, 1.02, 1.02)' : 'scale3d(1, 1, 1)'}`,
          transition: isHovering 
            ? 'transform 0.15s cubic-bezier(0.2, 0, 0, 1)' 
            : 'transform 0.8s cubic-bezier(0.2, 0.8, 0.2, 1)',
        }}
      >
        {/* Dynamic shadow/glow that moves opposite to the card tilt */}
        <div 
          className="absolute -inset-8 -z-10 rounded-[2rem] bg-black/5 opacity-0 blur-3xl transition-opacity duration-500 motion-reduce:hidden dark:bg-white/5"
          style={{
            transform: `translate3d(calc(var(--rotate-y) * -1px), calc(var(--rotate-x) * 1px), -20px)`,
            opacity: isHovering ? 1 : 0
          }}
        />

        <div className="relative overflow-hidden rounded-xl bg-background/5 shadow-2xl ring-1 ring-border/50 [transform-style:preserve-3d]">
          {children}
          
          {/* Specular glare effect */}
          <div 
            className="pointer-events-none absolute inset-0 z-50 mix-blend-overlay transition-opacity duration-300"
            style={{
              background: `radial-gradient(circle 300px at var(--glare-x) var(--glare-y), rgba(255, 255, 255, 0.6) 0%, rgba(255, 255, 255, 0) 100%)`,
              opacity: 'var(--glare-opacity)'
            }}
          />
          
          {/* Soft light sheen reflection */}
          <div 
            className="pointer-events-none absolute inset-0 z-50 mix-blend-soft-light transition-opacity duration-300"
            style={{
              background: `radial-gradient(circle 600px at var(--glare-x) var(--glare-y), rgba(255, 255, 255, 0.2) 0%, transparent 100%)`,
              opacity: 'calc(var(--glare-opacity) * 0.5)'
            }}
          />
        </div>
      </div>
    </div>
  )
}
