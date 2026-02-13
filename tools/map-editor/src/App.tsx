import AppHeader from './components/layout/AppHeader'
import CanvasRegion from './components/layout/CanvasRegion'
import LeftSidebar from './components/layout/LeftSidebar'

function App() {
  return (
    <div className="flex h-screen flex-col bg-[var(--bg-shell)]">
      <AppHeader />
      <main className="relative flex min-h-0 flex-1 overflow-hidden">
        <LeftSidebar />
        <CanvasRegion />
      </main>
    </div>
  )
}

export default App
