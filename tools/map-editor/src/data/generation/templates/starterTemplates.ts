import type { GenerationTemplate, GenerationTemplateId } from '../../../types/generation'

export const GENERATION_STARTER_TEMPLATES: GenerationTemplate[] = [
  {
    id: 'compact_town_spine',
    label: 'Compact Town Spine',
    description: 'Centered route spine with a compact west-side town core and balanced encounters.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.35,
        maxYRatio: 0.65,
        meanderChance: 0.28,
      },
      town: {
        anchorXRatio: 0.18,
        anchorYRatio: 0.5,
        widthRatio: 0.28,
        heightRatio: 0.46,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'northern_crossing',
    label: 'Northern Crossing',
    description: 'Upper-route traversal with a north-leaning town placement and east-heavy encounters.',
    hints: {
      primaryPath: {
        startYRatio: 0.32,
        minYRatio: 0.16,
        maxYRatio: 0.5,
        meanderChance: 0.4,
      },
      town: {
        anchorXRatio: 0.22,
        anchorYRatio: 0.34,
        widthRatio: 0.32,
        heightRatio: 0.44,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'southern_wilds',
    label: 'Southern Wilds',
    description: 'Lower-route arc with broader town footprint and west-weighted encounter fields.',
    hints: {
      primaryPath: {
        startYRatio: 0.68,
        minYRatio: 0.45,
        maxYRatio: 0.84,
        meanderChance: 0.34,
      },
      town: {
        anchorXRatio: 0.16,
        anchorYRatio: 0.66,
        widthRatio: 0.34,
        heightRatio: 0.5,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'central_switchbacks',
    label: 'Central Switchbacks',
    description: 'Mid-map switchback route with medium meander and a compact central town block.',
    hints: {
      primaryPath: {
        startYRatio: 0.52,
        minYRatio: 0.26,
        maxYRatio: 0.74,
        meanderChance: 0.46,
      },
      town: {
        anchorXRatio: 0.28,
        anchorYRatio: 0.5,
        widthRatio: 0.26,
        heightRatio: 0.38,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'riverbend_market',
    label: 'Riverbend Market',
    description: 'Path bends north then east; larger riverside town with east-leaning encounters.',
    hints: {
      primaryPath: {
        startYRatio: 0.44,
        minYRatio: 0.24,
        maxYRatio: 0.66,
        meanderChance: 0.38,
      },
      town: {
        anchorXRatio: 0.2,
        anchorYRatio: 0.46,
        widthRatio: 0.36,
        heightRatio: 0.52,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'cliffside_detour',
    label: 'Cliffside Detour',
    description: 'Upper-lane detour with tighter vertical movement and a smaller west ridge town.',
    hints: {
      primaryPath: {
        startYRatio: 0.3,
        minYRatio: 0.12,
        maxYRatio: 0.48,
        meanderChance: 0.32,
      },
      town: {
        anchorXRatio: 0.14,
        anchorYRatio: 0.3,
        widthRatio: 0.24,
        heightRatio: 0.34,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'eastward_promontory',
    label: 'Eastward Promontory',
    description: 'Route trends toward the east edge, with a modest town plateau and eastern wild zones.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.3,
        maxYRatio: 0.7,
        meanderChance: 0.24,
      },
      town: {
        anchorXRatio: 0.3,
        anchorYRatio: 0.54,
        widthRatio: 0.3,
        heightRatio: 0.4,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'westwood_weave',
    label: 'Westwood Weave',
    description: 'Forest-edge weave pattern with heavier meander and broad west-side encounter pockets.',
    hints: {
      primaryPath: {
        startYRatio: 0.56,
        minYRatio: 0.3,
        maxYRatio: 0.82,
        meanderChance: 0.5,
      },
      town: {
        anchorXRatio: 0.18,
        anchorYRatio: 0.58,
        widthRatio: 0.32,
        heightRatio: 0.46,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'twin_meadow_lane',
    label: 'Twin Meadow Lane',
    description: 'Gentle dual-lane feel with low meander and a wider but shallower town footprint.',
    hints: {
      primaryPath: {
        startYRatio: 0.48,
        minYRatio: 0.34,
        maxYRatio: 0.62,
        meanderChance: 0.2,
      },
      town: {
        anchorXRatio: 0.24,
        anchorYRatio: 0.48,
        widthRatio: 0.4,
        heightRatio: 0.32,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'lowland_bypass',
    label: 'Lowland Bypass',
    description: 'Lower bypass route hugging southern terrain with mid-size town and west encounter bias.',
    hints: {
      primaryPath: {
        startYRatio: 0.7,
        minYRatio: 0.52,
        maxYRatio: 0.86,
        meanderChance: 0.3,
      },
      town: {
        anchorXRatio: 0.22,
        anchorYRatio: 0.68,
        widthRatio: 0.3,
        heightRatio: 0.42,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'highland_sweep',
    label: 'Highland Sweep',
    description: 'Northern highland sweep with broad arc movement, larger hill town, and eastern encounters.',
    hints: {
      primaryPath: {
        startYRatio: 0.28,
        minYRatio: 0.1,
        maxYRatio: 0.46,
        meanderChance: 0.42,
      },
      town: {
        anchorXRatio: 0.26,
        anchorYRatio: 0.3,
        widthRatio: 0.34,
        heightRatio: 0.5,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'winding_passages',
    label: 'Winding Passages',
    description: 'Deep cave with high meander paths and scattered encounter zones.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.2,
        maxYRatio: 0.8,
        meanderChance: 0.6,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'crystal_depths',
    label: 'Crystal Depths',
    description: 'Lower cave region with rare item placement and eastern encounter clusters.',
    hints: {
      primaryPath: {
        startYRatio: 0.6,
        minYRatio: 0.4,
        maxYRatio: 0.9,
        meanderChance: 0.5,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'ocean_crossing',
    label: 'Ocean Crossing',
    description: 'Direct water route with minimal meander across open sea.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.35,
        maxYRatio: 0.65,
        meanderChance: 0.15,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'archipelago_drift',
    label: 'Archipelago Drift',
    description: 'Winding water path through island chains with western landing points.',
    hints: {
      primaryPath: {
        startYRatio: 0.45,
        minYRatio: 0.25,
        maxYRatio: 0.75,
        meanderChance: 0.4,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'abyssal_trench',
    label: 'Abyssal Trench',
    description: 'Deep underwater trench with rare encounters in darker depths.',
    hints: {
      primaryPath: {
        startYRatio: 0.55,
        minYRatio: 0.4,
        maxYRatio: 0.8,
        meanderChance: 0.35,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'coral_reef',
    label: 'Coral Reef',
    description: 'Shallow underwater area with scattered item zones and balanced paths.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.3,
        maxYRatio: 0.7,
        meanderChance: 0.25,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'lava_flows',
    label: 'Lava Flows',
    description: 'Volcano interior with narrow safe paths through lava fields.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.3,
        maxYRatio: 0.7,
        meanderChance: 0.3,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'crater_rim',
    label: 'Crater Rim',
    description: 'Volcanic crater edge with western entry and dangerous eastern zones.',
    hints: {
      primaryPath: {
        startYRatio: 0.4,
        minYRatio: 0.2,
        maxYRatio: 0.6,
        meanderChance: 0.45,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'beachfront_row',
    label: 'Beachfront Row',
    description: 'Coastal boardwalk with town structures along the western shore.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.35,
        maxYRatio: 0.65,
        meanderChance: 0.2,
      },
      town: {
        anchorXRatio: 0.2,
        anchorYRatio: 0.5,
        widthRatio: 0.35,
        heightRatio: 0.5,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'pier_plaza',
    label: 'Pier Plaza',
    description: 'Extended pier structure with shops and eastern water access.',
    hints: {
      primaryPath: {
        startYRatio: 0.48,
        minYRatio: 0.38,
        maxYRatio: 0.58,
        meanderChance: 0.15,
      },
      town: {
        anchorXRatio: 0.15,
        anchorYRatio: 0.48,
        widthRatio: 0.4,
        heightRatio: 0.45,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'forest_maze',
    label: 'Forest Maze',
    description: 'Dense forest with high meander and scattered hidden items.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.15,
        maxYRatio: 0.85,
        meanderChance: 0.65,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'canopy_trail',
    label: 'Canopy Trail',
    description: 'Forest route with northern bias and concentrated encounter zones.',
    hints: {
      primaryPath: {
        startYRatio: 0.35,
        minYRatio: 0.2,
        maxYRatio: 0.55,
        meanderChance: 0.5,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'gym_leader_hall',
    label: 'Gym Leader Hall',
    description: 'Symmetrical gym layout with central path to leader position.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.4,
        maxYRatio: 0.6,
        meanderChance: 0.1,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'trainer_gauntlet',
    label: 'Trainer Gauntlet',
    description: 'Gym with southern entry and trainer gauntlet to northern leader.',
    hints: {
      primaryPath: {
        startYRatio: 0.7,
        minYRatio: 0.3,
        maxYRatio: 0.85,
        meanderChance: 0.2,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'tower_arena',
    label: 'Tower Arena',
    description: 'Battle tower main floor with central arena and western entrance.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.35,
        maxYRatio: 0.65,
        meanderChance: 0.15,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'tower_basement',
    label: 'Tower Basement',
    description: 'Underground battle facility with maze-like layout.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.25,
        maxYRatio: 0.75,
        meanderChance: 0.4,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'summit_path',
    label: 'Summit Path',
    description: 'Steep mountain ascent with western base and eastern peak.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.2,
        maxYRatio: 0.8,
        meanderChance: 0.55,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'cliff_edge',
    label: 'Cliff Edge',
    description: 'Mountain ridge route with northern cliff edge and rare encounters.',
    hints: {
      primaryPath: {
        startYRatio: 0.3,
        minYRatio: 0.15,
        maxYRatio: 0.5,
        meanderChance: 0.35,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'lunar_surface',
    label: 'Lunar Surface',
    description: 'Alien moon terrain with scattered craters and western landing zone.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.3,
        maxYRatio: 0.7,
        meanderChance: 0.4,
      },
      town: {
        anchorXRatio: 0.15,
        anchorYRatio: 0.5,
        widthRatio: 0.25,
        heightRatio: 0.4,
      },
      encounterZone: 'east',
    },
  },
  {
    id: 'crater_base',
    label: 'Crater Base',
    description: 'Deep lunar crater with central exploration and rare item zones.',
    hints: {
      primaryPath: {
        startYRatio: 0.55,
        minYRatio: 0.35,
        maxYRatio: 0.75,
        meanderChance: 0.5,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'cruise_deck',
    label: 'Cruise Deck',
    description: 'Ship main deck with western boarding and eastern viewing areas.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.4,
        maxYRatio: 0.6,
        meanderChance: 0.1,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'cargo_hold',
    label: 'Cargo Hold',
    description: 'Ship interior with maze-like cargo arrangement and item zones.',
    hints: {
      primaryPath: {
        startYRatio: 0.5,
        minYRatio: 0.3,
        maxYRatio: 0.7,
        meanderChance: 0.45,
      },
      encounterZone: 'west',
    },
  },
  {
    id: 'zone_north',
    label: 'Zone North',
    description: 'Safari zone northern sector with open encounter fields.',
    hints: {
      primaryPath: {
        startYRatio: 0.35,
        minYRatio: 0.15,
        maxYRatio: 0.5,
        meanderChance: 0.35,
      },
      encounterZone: 'balanced',
    },
  },
  {
    id: 'zone_south',
    label: 'Zone South',
    description: 'Safari zone southern sector with rare encounter concentration.',
    hints: {
      primaryPath: {
        startYRatio: 0.65,
        minYRatio: 0.5,
        maxYRatio: 0.85,
        meanderChance: 0.4,
      },
      encounterZone: 'east',
    },
  },
]

export const GENERATION_STARTER_TEMPLATES_BY_ID: Record<GenerationTemplateId, GenerationTemplate> =
  GENERATION_STARTER_TEMPLATES.reduce<Record<GenerationTemplateId, GenerationTemplate>>((accumulator, template) => {
    accumulator[template.id] = template
    return accumulator
  }, {} as Record<GenerationTemplateId, GenerationTemplate>)
