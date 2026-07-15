import { getGlassPreset } from './GlassSurface'

describe('getGlassPreset', () => {
  it('keeps passive surfaces low cost and readable', () => {
    expect(getGlassPreset('subtle')).toMatchObject({
      displacementScale: 10,
      aberrationIntensity: 0.25,
      elasticity: 0,
      cornerRadius: 24,
      mode: 'standard',
    })
  })

  it('uses stronger feedback only for interactive surfaces', () => {
    const preset = getGlassPreset('interactive')

    expect(preset.displacementScale).toBeGreaterThan(20)
    expect(preset.elasticity).toBeGreaterThan(0)
    expect(preset.cornerRadius).toBe(18)
  })
})
