import { PanelLeftClose, PanelLeftOpen, X } from 'lucide-react'
import { useRandomGenerationControls } from '../../hooks/useRandomGenerationControls'
import { useSidebarControls } from '../../hooks/useSidebarControls'
import { useUiStore } from '../../store/uiStore'
import BuildingControlsSection from '../sidebar/BuildingControlsSection'
import MapDimensionsSection from '../sidebar/MapDimensionsSection'
import PaletteSection from '../sidebar/PaletteSection'
import RandomGenerationSection from '../sidebar/RandomGenerationSection'

function SidebarBody() {
  const useDistinctColors = useUiStore((state) => state.useDistinctColors)
  const setUseDistinctColors = useUiStore((state) => state.setUseDistinctColors)

  const {
    selectedCategory,
    selectedTileId,
    selectedBuildingId,
    buildingRotation,
    mapName,
    widthInput,
    heightInput,
    cellSize,
    visibleTiles,
    buildings,
    selectedBuilding,
    rotatedBuilding,
    setMapName,
    setWidthInput,
    setHeightInput,
    setCellSize,
    resizeGridFromInputs,
    clearGrid,
    handleSelectCategory,
    handleSelectTile,
    handleToggleBuilding,
    handleRotateBuilding,
    handleResetSaved,
  } = useSidebarControls()

  const {
    archetypeOptions,
    templateOptions,
    currentMapWidth,
    currentMapHeight,
    generationSeedInput,
    generationArchetypeId,
    generationTemplateId,
    generationWidthInput,
    generationHeightInput,
    generationUseCurrentDimensions,
    generationMaxRepairAttempts,
    generationEnforceSpawnSafety,
    generationEnforceDoorConnectivity,
    lastGeneratedSeed,
    lastGenerationDiagnostics,
    diagnosticsSummary,
    canRegenerate,
    setGenerationSeedInput,
    setGenerationArchetypeId,
    setGenerationTemplateId,
    setGenerationWidthInput,
    setGenerationHeightInput,
    setGenerationUseCurrentDimensions,
    setGenerationMaxRepairAttempts,
    setGenerationEnforceSpawnSafety,
    setGenerationEnforceDoorConnectivity,
    generateRandomMapFromControls,
    regenerateRandomMap,
  } = useRandomGenerationControls()

  return (
    <div className="space-y-3 overflow-y-auto p-3">
      <PaletteSection
        selectedCategory={selectedCategory}
        selectedTileId={selectedTileId}
        selectedBuildingId={selectedBuildingId}
        visibleTiles={visibleTiles}
        buildings={buildings}
        handleSelectCategory={handleSelectCategory}
        handleSelectTile={handleSelectTile}
        handleToggleBuilding={handleToggleBuilding}
        useDistinctColors={useDistinctColors}
        setUseDistinctColors={setUseDistinctColors}
      />

      <MapDimensionsSection
        mapName={mapName}
        widthInput={widthInput}
        heightInput={heightInput}
        cellSize={cellSize}
        setMapName={setMapName}
        setWidthInput={setWidthInput}
        setHeightInput={setHeightInput}
        setCellSize={setCellSize}
        resizeGridFromInputs={resizeGridFromInputs}
        clearGrid={clearGrid}
        handleResetSaved={handleResetSaved}
      />

      <RandomGenerationSection
        archetypeOptions={archetypeOptions}
        templateOptions={templateOptions}
        currentMapWidth={currentMapWidth}
        currentMapHeight={currentMapHeight}
        generationSeedInput={generationSeedInput}
        generationArchetypeId={generationArchetypeId}
        generationTemplateId={generationTemplateId}
        generationWidthInput={generationWidthInput}
        generationHeightInput={generationHeightInput}
        generationUseCurrentDimensions={generationUseCurrentDimensions}
        generationMaxRepairAttempts={generationMaxRepairAttempts}
        generationEnforceSpawnSafety={generationEnforceSpawnSafety}
        generationEnforceDoorConnectivity={generationEnforceDoorConnectivity}
        lastGeneratedSeed={lastGeneratedSeed}
        lastGenerationDiagnostics={lastGenerationDiagnostics}
        diagnosticsSummary={diagnosticsSummary}
        canRegenerate={canRegenerate}
        setGenerationSeedInput={setGenerationSeedInput}
        setGenerationArchetypeId={setGenerationArchetypeId}
        setGenerationTemplateId={setGenerationTemplateId}
        setGenerationWidthInput={setGenerationWidthInput}
        setGenerationHeightInput={setGenerationHeightInput}
        setGenerationUseCurrentDimensions={setGenerationUseCurrentDimensions}
        setGenerationMaxRepairAttempts={setGenerationMaxRepairAttempts}
        setGenerationEnforceSpawnSafety={setGenerationEnforceSpawnSafety}
        setGenerationEnforceDoorConnectivity={setGenerationEnforceDoorConnectivity}
        generateRandomMapFromControls={generateRandomMapFromControls}
        regenerateRandomMap={regenerateRandomMap}
      />

      <BuildingControlsSection
        selectedBuildingId={selectedBuildingId}
        buildingRotation={buildingRotation}
        selectedBuilding={selectedBuilding}
        rotatedBuilding={rotatedBuilding}
        handleRotateBuilding={handleRotateBuilding}
      />
      <div className="pb-1" />
    </div>
  )
}

function LeftSidebar() {
  const isSidebarOpen = useUiStore((state) => state.isSidebarOpen)
  const toggleSidebar = useUiStore((state) => state.toggleSidebar)
  const setSidebarOpen = useUiStore((state) => state.setSidebarOpen)

  return (
    <>
      {isSidebarOpen && (
        <button
          type="button"
          className="fixed inset-0 z-20 bg-[#0f17145c] md:hidden"
          aria-label="Close sidebar"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      <aside
        className={[
          'hidden shrink-0 border-r border-[var(--border-soft)] bg-[var(--surface)] transition-[width] duration-200 md:flex md:flex-col',
          isSidebarOpen ? 'w-72' : 'w-12',
        ].join(' ')}
      >
        <div
          className={[
            'flex h-10 items-center border-b border-[var(--border-soft)] px-2',
            isSidebarOpen ? 'justify-end' : 'justify-center',
          ].join(' ')}
        >
          <button
            type="button"
            className="grid h-7 w-7 place-items-center rounded text-[var(--text-muted)] hover:bg-[var(--surface-muted)] hover:text-[var(--text-main)]"
            aria-label={isSidebarOpen ? 'Collapse sidebar' : 'Expand sidebar'}
            onClick={toggleSidebar}
          >
            {isSidebarOpen ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
          </button>
        </div>
        {isSidebarOpen ? <SidebarBody /> : null}
      </aside>

      <aside
        className={[
          'fixed left-0 top-0 z-30 h-full w-72 border-r border-[var(--border-soft)] bg-[var(--surface)] transition-transform duration-200 md:hidden',
          isSidebarOpen ? 'translate-x-0' : '-translate-x-full',
        ].join(' ')}
      >
        <div className="flex h-10 items-center justify-between border-b border-[var(--border-soft)] px-2">
          <button
            type="button"
            className="grid h-7 w-7 place-items-center rounded text-[var(--text-muted)] hover:bg-[var(--surface-muted)] hover:text-[var(--text-main)]"
            aria-label="Collapse sidebar"
            onClick={() => setSidebarOpen(false)}
          >
            <PanelLeftClose size={16} />
          </button>
          <button
            type="button"
            className="grid h-7 w-7 place-items-center rounded text-[var(--text-muted)] hover:bg-[var(--surface-muted)] hover:text-[var(--text-main)]"
            aria-label="Close sidebar"
            onClick={() => setSidebarOpen(false)}
          >
            <X size={16} />
          </button>
        </div>
        <SidebarBody />
      </aside>
    </>
  )
}

export default LeftSidebar
