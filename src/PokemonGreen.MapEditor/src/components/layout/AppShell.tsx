import { MenuBar } from './MenuBar'
import { Sidebar } from './Sidebar'
import { CanvasContainer } from './CanvasContainer'
import { PropertiesPanel } from './PropertiesPanel'

export function AppShell() {
  return (
    <div className="h-full flex flex-col">
      <MenuBar />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar />
        <CanvasContainer />
        <PropertiesPanel />
      </div>
    </div>
  )
}
