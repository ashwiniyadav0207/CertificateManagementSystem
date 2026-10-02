"use client"

import PrismaticBurst from "@/components/effects/prismatic-burst"

export function AuthBackground() {
  return (
    <div className="pointer-events-auto absolute inset-0 -z-0">
      <PrismaticBurst
        animationType="hover"
        intensity={1.4}
        speed={0.28}
        distort={0.25}
        rayCount={6}
        hoverDampness={0.35}
        colors={["#FFD700", "#FFA500", "#F5A623", "#d99a3d"]}
        className="opacity-50"
      />
      <div className="absolute inset-0 bg-noise opacity-30" />
    </div>
  )
}
