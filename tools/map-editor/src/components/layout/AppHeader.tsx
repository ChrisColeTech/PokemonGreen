import { Menu, Minus, Square, X } from 'lucide-react'
import { useState } from 'react'
import { useMenuActions } from '../../hooks/useMenuActions'
import { useUiStore } from '../../store/uiStore'

type MenuKey = 'file' | 'edit' | 'view' | null

function AppHeader() {
  const toggleSidebar = useUiStore((state) => state.toggleSidebar)
  const [openMenu, setOpenMenu] = useState<MenuKey>(null)
  const { actions, state } = useMenuActions()

  const runMenuAction =
    (action: () => void | Promise<void>) =>
    async (): Promise<void> => {
      setOpenMenu(null)
      await action()
    }

  const menuButtonClass =
    'inline-flex h-full items-center px-3 text-xs font-medium uppercase tracking-wide text-[var(--text-main)] hover:bg-[var(--surface-muted)]'

  const menuItemClass =
    'w-full rounded px-2 py-1.5 text-left text-xs text-[var(--text-main)] hover:bg-[var(--surface-muted)]'

  return (
    <header className="border-b border-[var(--border-soft)] bg-[var(--surface)]">
      <div className="flex h-10 items-center justify-between gap-2">
        <div className="flex h-full min-w-0 items-stretch gap-1">
          <button
            type="button"
            className="grid h-full w-9 place-items-center text-[var(--text-main)] hover:bg-[var(--surface-muted)] md:hidden"
            aria-label="Toggle sidebar"
            onClick={toggleSidebar}
          >
            <Menu size={16} />
          </button>

          <div className="ml-1 flex h-7 w-7 self-center items-center justify-center rounded border border-[var(--border-strong)] bg-[var(--surface-muted)] text-[10px] font-black text-[var(--accent-strong)]">
            MG
          </div>

          <div className="relative ml-2 hidden h-full sm:block">
            <button type="button" className={menuButtonClass} onClick={() => setOpenMenu(openMenu === 'file' ? null : 'file')}>
              File
            </button>
            {openMenu === 'file' ? (
              <div className="absolute left-0 top-8 z-40 w-44 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-1 shadow-lg">
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleNewMap)}>New Map</button>
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleOpenMap)}>Open Map</button>
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleImportJson)}>Import Map JSON</button>
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleExportJson)}>Export</button>
                <div className="my-1 border-t border-[var(--border-soft)]" />
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleSave)}>Save</button>
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleSaveAs)}>Save As</button>
              </div>
            ) : null}
          </div>

          <div className="relative hidden h-full sm:block">
            <button type="button" className={menuButtonClass} onClick={() => setOpenMenu(openMenu === 'edit' ? null : 'edit')}>
              Edit
            </button>
            {openMenu === 'edit' ? (
              <div className="absolute left-0 top-8 z-40 w-36 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-1 shadow-lg">
                <button type="button" className={menuItemClass} disabled={!state.canUndo} onClick={runMenuAction(actions.handleUndo)}>Undo</button>
                <button type="button" className={menuItemClass} disabled={!state.canRedo} onClick={runMenuAction(actions.handleRedo)}>Redo</button>
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleClear)}>Clear</button>
              </div>
            ) : null}
          </div>

          <div className="relative hidden h-full sm:block">
            <button type="button" className={menuButtonClass} onClick={() => setOpenMenu(openMenu === 'view' ? null : 'view')}>
              View
            </button>
            {openMenu === 'view' ? (
              <div className="absolute left-0 top-8 z-40 w-36 rounded-md border border-[var(--border-soft)] bg-[var(--surface)] p-1 shadow-lg">
                <button type="button" className={menuItemClass} disabled={!state.canZoomIn} onClick={runMenuAction(actions.handleZoomIn)}>Zoom In</button>
                <button type="button" className={menuItemClass} disabled={!state.canZoomOut} onClick={runMenuAction(actions.handleZoomOut)}>Zoom Out</button>
                <button type="button" className={menuItemClass} onClick={runMenuAction(actions.handleResetZoom)}>Reset Zoom</button>
              </div>
            ) : null}
          </div>
        </div>

        <div className="hidden h-full md:flex">
          <button
            type="button"
            className="grid h-full w-11 place-items-center text-[var(--text-muted)] hover:bg-[var(--surface-muted)] hover:text-[var(--text-main)]"
            aria-label="Minimize"
          >
            <Minus size={14} />
          </button>
          <button
            type="button"
            className="grid h-full w-11 place-items-center text-[var(--text-muted)] hover:bg-[var(--surface-muted)] hover:text-[var(--text-main)]"
            aria-label="Maximize"
          >
            <Square size={12} />
          </button>
          <button
            type="button"
            className="grid h-full w-11 place-items-center text-[var(--text-muted)] hover:bg-[#7a2d34] hover:text-white"
            aria-label="Close"
          >
            <X size={14} />
          </button>
        </div>
      </div>
    </header>
  )
}

export default AppHeader
