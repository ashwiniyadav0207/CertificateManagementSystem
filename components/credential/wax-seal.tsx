export function WaxSeal({ accent, className }: { accent: string; className?: string }) {
  const scallops = Array.from({ length: 14 })
  return (
    <div className={`seal size-24 sm:size-28 ${className ?? ""}`} aria-hidden="true">
      <svg viewBox="0 0 120 120" className="size-full drop-shadow-[0_10px_18px_rgb(0_0_0/0.25)]">
        <g className="seal-spin" style={{ transformOrigin: "60px 60px" }}>
          <circle
            cx="60"
            cy="60"
            r="46"
            fill="none"
            stroke={accent}
            strokeWidth="1.5"
            strokeDasharray="3 5"
            opacity="0.55"
          />
        </g>
        {scallops.map((_, i) => {
          const angle = (i / scallops.length) * Math.PI * 2
          const cx = 60 + Math.cos(angle) * 34
          const cy = 60 + Math.sin(angle) * 34
          return <circle key={i} cx={cx} cy={cy} r="7.5" fill={accent} />
        })}
        <circle cx="60" cy="60" r="34" fill={accent} />
        <circle cx="60" cy="60" r="27" fill="none" stroke="rgb(255 255 255 / 0.35)" strokeWidth="1" />
        <path
          d="M45 61l10 10 20-22"
          fill="none"
          stroke="rgb(255 255 255 / 0.92)"
          strokeWidth="5"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <path d="M44 92l8-14 8 6-10 16z" fill={accent} opacity="0.9" />
        <path d="M76 92l-8-14-8 6 10 16z" fill={accent} opacity="0.9" />
      </svg>
    </div>
  )
}
