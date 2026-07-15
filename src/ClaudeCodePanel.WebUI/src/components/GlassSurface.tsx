import type { CSSProperties, HTMLAttributes, PropsWithChildren } from 'react'
import LiquidGlass from 'liquid-glass-react'

export type GlassPresetName = 'subtle' | 'interactive' | 'prominent' | 'navigation'

export interface GlassPreset {
  displacementScale: number
  blurAmount: number
  saturation: number
  aberrationIntensity: number
  elasticity: number
  cornerRadius: number
  mode: 'standard' | 'prominent'
}

const glassPresets: Record<GlassPresetName, GlassPreset> = {
  subtle: {
    displacementScale: 10,
    blurAmount: 0.045,
    saturation: 112,
    aberrationIntensity: 0.25,
    elasticity: 0,
    cornerRadius: 24,
    mode: 'standard',
  },
  interactive: {
    displacementScale: 26,
    blurAmount: 0.075,
    saturation: 126,
    aberrationIntensity: 0.65,
    elasticity: 0.12,
    cornerRadius: 18,
    mode: 'standard',
  },
  prominent: {
    displacementScale: 16,
    blurAmount: 0.08,
    saturation: 122,
    aberrationIntensity: 0.4,
    elasticity: 0.03,
    cornerRadius: 28,
    mode: 'prominent',
  },
  navigation: {
    displacementScale: 12,
    blurAmount: 0.06,
    saturation: 118,
    aberrationIntensity: 0.3,
    elasticity: 0.02,
    cornerRadius: 30,
    mode: 'standard',
  },
}

export const getGlassPreset = (name: GlassPresetName) => glassPresets[name]

interface GlassSurfaceProps extends PropsWithChildren {
  preset?: GlassPresetName
  className?: string
  style?: CSSProperties
  padding?: string
  overLight?: boolean
  onClick?: () => void
  fallback?: boolean
  ariaLabel?: string
  role?: HTMLAttributes<HTMLElement>['role']
}

export function GlassSurface({
  children,
  preset = 'subtle',
  className = '',
  style,
  padding,
  overLight = false,
  onClick,
  fallback = false,
  ariaLabel,
  role,
}: GlassSurfaceProps) {
  const config = glassPresets[preset]
  const classes = `glass-surface glass-surface--${preset} ${className}`.trim()
  const reduceEffects = typeof window !== 'undefined' &&
    window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true

  if (fallback || reduceEffects) {
    return (
      <div className={`${classes} glass-surface--fallback glass-frame`} style={style} role={role} aria-label={ariaLabel}>
        <div className="glass-surface__content">{children}</div>
      </div>
    )
  }

  return (
    <div className={`glass-frame ${classes}`.trim()} style={style} role={role} aria-label={ariaLabel} onClick={onClick}>
      <div className="glass-surface__material" aria-hidden="true">
        <LiquidGlass
          {...config}
          className="glass-material"
          style={{ position: 'absolute', left: '50%', top: '50%' }}
          padding={padding}
          overLight={overLight}
        >
          <div className="glass-material__sizer" />
        </LiquidGlass>
      </div>
      <div className="glass-surface__content">
        {children}
      </div>
    </div>
  )
}
