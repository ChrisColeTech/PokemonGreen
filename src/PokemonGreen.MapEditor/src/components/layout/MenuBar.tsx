import { useState, useEffect, useRef } from 'react'
import { useEditorStore } from '../../store/editorStore'
import { parseRegistryJson, parseCSharpRegistry } from '../../services/registryService'

interface MenuItem {
  label: string
  shortcut?: string
  disabled?: boolean
  danger?: boolean
  separator?: boolean
  onClick?: () => void
}

interface MenuDefinition {
  label: string
  items: MenuItem[]
}

function downloadFile(filename: string, content: string) {
  const blob = new Blob([content], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export function MenuBar() {
  const [openIndex, setOpenIndex] = useState<number | null>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const csMapInputRef = useRef<HTMLInputElement>(null)
  const registryInputRef = useRef<HTMLInputElement>(null)
  const csRegistryInputRef = useRef<HTMLInputElement>(null)

  const mapName = useEditorStore(s => s.mapName)
  const importJson = useEditorStore(s => s.importJson)
  const exportJson = useEditorStore(s => s.exportJson)
  const importCSharpMap = useEditorStore(s => s.importCSharpMap)
  const clear = useEditorStore(s => s.clear)
  const setRegistry = useEditorStore(s => s.setRegistry)

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpenIndex(null)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  function handleImport() {
    fileInputRef.current?.click()
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      importJson(ev.target?.result as string)
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  function handleImportCSharp() {
    csMapInputRef.current?.click()
  }

  function handleCSharpMapChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      importCSharpMap(ev.target?.result as string)
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  function handleExportJson() {
    const json = exportJson()
    const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')
    downloadFile(`${mapId}.map.json`, json)
  }

  function handleLoadRegistry() {
    registryInputRef.current?.click()
  }

  function handleRegistryChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      try {
        const registry = parseRegistryJson(ev.target?.result as string)
        setRegistry(registry)
      } catch (err) {
        alert(`Invalid registry: ${err instanceof Error ? err.message : 'Unknown error'}`)
      }
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  function handleLoadCSharpRegistry() {
    csRegistryInputRef.current?.click()
  }

  function handleCSharpRegistryChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (ev) => {
      try {
        const registry = parseCSharpRegistry(ev.target?.result as string)
        setRegistry(registry)
      } catch (err) {
        alert(`Invalid C# registry: ${err instanceof Error ? err.message : 'Unknown error'}`)
      }
    }
    reader.readAsText(file)
    e.target.value = ''
  }

  const menus: MenuDefinition[] = [
    {
      label: 'File',
      items: [
        { label: 'New Map', shortcut: 'Ctrl+N', onClick: clear },
        { separator: true, label: '' },
        { label: 'Import Map (JSON)...', onClick: handleImport },
        { label: 'Import Map (C#)...', onClick: handleImportCSharp },
        { label: 'Export JSON', onClick: handleExportJson },
        { separator: true, label: '' },
        { label: 'Load Registry (JSON)...', onClick: handleLoadRegistry },
        { label: 'Load Registry (C#)...', onClick: handleLoadCSharpRegistry },
      ],
    },
    {
      label: 'Edit',
      items: [
        { label: 'Undo', shortcut: 'Ctrl+Z', disabled: true },
        { label: 'Redo', shortcut: 'Ctrl+Y', disabled: true },
        { separator: true, label: '' },
        { label: 'Clear All', onClick: clear },
      ],
    },
    {
      label: 'View',
      items: [
        { label: 'Show Grid' },
        { label: 'Show Trainer Vision' },
      ],
    },
  ]

  return (
    <div
      ref={menuRef}
      className="h-[30px] bg-[#1e1e1e] border-b border-[#2d2d2d] flex items-center select-none"
      style={{ fontSize: '13px' }}
    >
      {menus.map((menu, i) => (
        <div key={menu.label} className="relative">
          <button
            className="h-[30px] px-[10px] bg-transparent border-none text-[#e0e0e0] cursor-pointer hover:bg-[#2d2d2d] text-[13px]"
            style={openIndex === i ? { background: '#2d2d2d' } : undefined}
            onClick={() => setOpenIndex(openIndex === i ? null : i)}
            onMouseEnter={() => { if (openIndex !== null) setOpenIndex(i) }}
          >
            {menu.label}
          </button>

          {openIndex === i && (
            <div className="absolute top-[30px] left-0 bg-[#1e1e1e] border border-[#2d2d2d] shadow-xl min-w-[220px] py-[4px] z-50">
              {menu.items.map((item, j) =>
                item.separator ? (
                  <div key={j} className="h-[1px] bg-[#2d2d2d] my-[4px] mx-[10px]" />
                ) : (
                  <button
                    key={j}
                    disabled={item.disabled}
                    className="w-full h-[28px] px-[20px] bg-transparent border-none text-left text-[13px] flex items-center justify-between cursor-pointer hover:bg-[#2d2d2d] disabled:opacity-40 disabled:cursor-default disabled:hover:bg-transparent"
                    style={{ color: item.danger ? '#c74e4e' : item.disabled ? '#555555' : '#e0e0e0' }}
                    onClick={() => {
                      item.onClick?.()
                      setOpenIndex(null)
                    }}
                  >
                    <span>{item.label}</span>
                    {item.shortcut && (
                      <span className="text-[#555555] text-[12px] ml-[30px]">{item.shortcut}</span>
                    )}
                  </button>
                ),
              )}
            </div>
          )}
        </div>
      ))}

      <div className="flex-1" />

      <input
        ref={fileInputRef}
        type="file"
        accept=".json"
        className="hidden"
        onChange={handleFileChange}
      />
      <input
        ref={csMapInputRef}
        type="file"
        accept=".cs"
        className="hidden"
        onChange={handleCSharpMapChange}
      />
      <input
        ref={registryInputRef}
        type="file"
        accept=".json"
        className="hidden"
        onChange={handleRegistryChange}
      />
      <input
        ref={csRegistryInputRef}
        type="file"
        accept=".cs"
        className="hidden"
        onChange={handleCSharpRegistryChange}
      />
    </div>
  )
}
