import { useState, useEffect } from 'react'
import { PanelLeftClose, PanelLeftOpen, RotateCcw, RotateCw } from 'lucide-react'
import { useEditorStore } from '../../store/editorStore'
import { buildingWidth, buildingHeight } from '../../services/registryService'

export function Sidebar() {
  const [collapsed, setCollapsed] = useState(false)

  const registry = useEditorStore(s => s.registry)
  const selectedTile = useEditorStore(s => s.selectedTile)
  const selectedBuilding = useEditorStore(s => s.selectedBuilding)
  const mapWidth = useEditorStore(s => s.mapWidth)
  const mapHeight = useEditorStore(s => s.mapHeight)
  const cellSize = useEditorStore(s => s.cellSize)
  const mapName = useEditorStore(s => s.mapName)
  const selectTile = useEditorStore(s => s.selectTile)
  const selectBuilding = useEditorStore(s => s.selectBuilding)
  const rotateBuilding = useEditorStore(s => s.rotateBuilding)
  const resize = useEditorStore(s => s.resize)
  const clear = useEditorStore(s => s.clear)
  const setCellSize = useEditorStore(s => s.setCellSize)
  const setMapName = useEditorStore(s => s.setMapName)

  const [inputWidth, setInputWidth] = useState(mapWidth)
  const [inputHeight, setInputHeight] = useState(mapHeight)

  // Sync local inputs when store dimensions change (e.g. after import)
  useEffect(() => {
    setInputWidth(mapWidth)
  }, [mapWidth])
  useEffect(() => {
    setInputHeight(mapHeight)
  }, [mapHeight])

  const paletteCategories = registry.categories.filter(c => c.showInPalette)

  if (collapsed) {
    return (
      <div className="w-[36px] bg-[#1e1e1e] border-r border-[#2d2d2d] flex flex-col items-center pt-[6px]">
        <button
          className="w-[28px] h-[28px] border-none rounded-[3px] cursor-pointer text-[#808080] bg-transparent hover:bg-[#2d2d2d] hover:text-[#e0e0e0] flex items-center justify-center"
          onClick={() => setCollapsed(false)}
          title="Expand sidebar"
        >
          <PanelLeftOpen size={16} />
        </button>
      </div>
    )
  }

  return (
    <div className="w-[280px] bg-[#1e1e1e] border-r border-[#2d2d2d] flex flex-col overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-[10px] h-[30px] border-b border-[#2d2d2d]">
        <span className="text-[11px] text-[#808080] uppercase tracking-wider">Palette</span>
        <button
          className="w-[22px] h-[22px] border-none rounded-[3px] cursor-pointer text-[#808080] bg-transparent hover:bg-[#2d2d2d] hover:text-[#e0e0e0] flex items-center justify-center"
          onClick={() => setCollapsed(true)}
          title="Collapse sidebar"
        >
          <PanelLeftClose size={14} />
        </button>
      </div>

      {/* Map Name */}
      <div className="px-[10px] py-[8px] border-b border-[#2d2d2d]">
        <label className="text-[11px] text-[#808080] block mb-[2px]">Map Name</label>
        <input
          type="text"
          value={mapName}
          onChange={(e) => setMapName(e.target.value)}
          className="w-full p-[5px] border border-[#2d2d2d] rounded-[3px] bg-[#161616] text-[#e0e0e0] text-[13px]"
        />
      </div>

      {/* Registry indicator */}
      <div className="px-[10px] py-[4px] border-b border-[#2d2d2d]">
        <span className="text-[10px] text-[#555555]">{registry.name} v{registry.version}</span>
      </div>

      {/* Tile Palette — dynamic from registry */}
      <div className="flex-1 overflow-y-auto p-[10px]">
        {paletteCategories.map((cat) => {
          const tiles = registry.tiles.filter(t => t.category === cat.id)
          if (tiles.length === 0) return null

          return (
            <div key={cat.id}>
              <div className="text-[11px] text-[#808080] mt-[10px] mb-[5px]">{cat.label}</div>
              <div className="grid grid-cols-2 gap-[4px]">
                {tiles.map((tile) => (
                  <button
                    key={tile.id}
                    className="p-[8px] rounded-[3px] cursor-pointer text-[12px] text-[#e0e0e0] text-center"
                    style={{
                      background: tile.color,
                      border: selectedTile === tile.id && selectedBuilding === null ? '2px solid #e0e0e0' : '2px solid transparent',
                    }}
                    onClick={() => selectTile(tile.id)}
                  >
                    {tile.name}
                  </button>
                ))}
              </div>
            </div>
          )
        })}

        {/* Buildings — dynamic from registry */}
        {registry.buildings.length > 0 && (
          <div>
            <div className="text-[11px] text-[#808080] mt-[10px] mb-[5px]">Buildings</div>
            <div className="grid grid-cols-2 gap-[4px]">
              {registry.buildings.map((building, idx) => {
                const w = buildingWidth(building)
                const h = buildingHeight(building)
                return (
                  <button
                    key={building.id}
                    className="p-[8px] rounded-[3px] cursor-pointer text-[10px] text-[#e0e0e0] text-center whitespace-pre-line"
                    style={{
                      background: '#2a4a3a',
                      border: selectedBuilding === idx ? '2px solid #e0e0e0' : '2px solid transparent',
                    }}
                    onClick={() => selectBuilding(idx)}
                  >
                    {building.name}{'\n'}{w}x{h}
                  </button>
                )
              })}
            </div>
          </div>
        )}
      </div>

      {/* Map Controls */}
      <div className="p-[10px] border-t border-[#2d2d2d] flex flex-col gap-[6px]">
        <div className="flex gap-[6px]">
          <div className="flex-1">
            <label className="text-[11px] text-[#808080] block mb-[2px]">Width</label>
            <input
              type="number"
              value={inputWidth}
              min={5}
              max={100}
              onChange={(e) => setInputWidth(Number(e.target.value))}
              className="w-full p-[5px] border border-[#2d2d2d] rounded-[3px] bg-[#161616] text-[#e0e0e0] text-[13px]"
            />
          </div>
          <div className="flex-1">
            <label className="text-[11px] text-[#808080] block mb-[2px]">Height</label>
            <input
              type="number"
              value={inputHeight}
              min={5}
              max={100}
              onChange={(e) => setInputHeight(Number(e.target.value))}
              className="w-full p-[5px] border border-[#2d2d2d] rounded-[3px] bg-[#161616] text-[#e0e0e0] text-[13px]"
            />
          </div>
          <div className="flex-1">
            <label className="text-[11px] text-[#808080] block mb-[2px]">Cell Size</label>
            <input
              type="number"
              value={cellSize}
              min={8}
              max={48}
              onChange={(e) => setCellSize(Number(e.target.value))}
              className="w-full p-[5px] border border-[#2d2d2d] rounded-[3px] bg-[#161616] text-[#e0e0e0] text-[13px]"
            />
          </div>
        </div>
        <button
          onClick={() => resize(inputWidth, inputHeight)}
          className="w-full p-[5px] border-none rounded-[3px] cursor-pointer text-[13px] text-[#e0e0e0] bg-[#2d2d2d] hover:bg-[#383838]"
        >
          Resize
        </button>
        <button
          onClick={() => { clear(); setInputWidth(25); setInputHeight(18) }}
          className="w-full p-[5px] border-none rounded-[3px] cursor-pointer text-[13px] text-[#e0e0e0] bg-[#2d2d2d] hover:bg-[#383838]"
        >
          Clear All
        </button>
        <div className="flex gap-[4px]">
          <button
            disabled={selectedBuilding === null}
            onClick={() => rotateBuilding(-1)}
            className="flex-1 p-[5px] border-none rounded-[3px] cursor-pointer text-[13px] text-[#e0e0e0] bg-[#2d2d2d] hover:bg-[#383838] disabled:opacity-40 disabled:cursor-default flex items-center justify-center gap-[4px]"
          >
            <RotateCcw size={14} /> Left
          </button>
          <button
            disabled={selectedBuilding === null}
            onClick={() => rotateBuilding(1)}
            className="flex-1 p-[5px] border-none rounded-[3px] cursor-pointer text-[13px] text-[#e0e0e0] bg-[#2d2d2d] hover:bg-[#383838] disabled:opacity-40 disabled:cursor-default flex items-center justify-center gap-[4px]"
          >
            <RotateCw size={14} /> Right
          </button>
        </div>
      </div>
    </div>
  )
}
