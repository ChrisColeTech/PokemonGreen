import { useCallback, useState } from 'react'
import { useEditorStore } from '../store/editorStore'

export default function DropZone() {
  const [dragging, setDragging] = useState(false)
  const loadFiles = useEditorStore(s => s.loadFiles)
  const loading = useEditorStore(s => s.loading)

  const handleDrop = useCallback(async (e: React.DragEvent) => {
    e.preventDefault()
    setDragging(false)

    const files = await collectAllFiles(e.dataTransfer)
    if (files.length > 0) loadFiles(files)
  }, [loadFiles])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragging(true)
  }, [])

  const handleDragLeave = useCallback(() => {
    setDragging(false)
  }, [])

  const handleFileInput = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? [])
    if (files.length > 0) loadFiles(files)
    e.target.value = ''
  }, [loadFiles])

  return (
    <div
      onDrop={handleDrop}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      style={{
        position: 'absolute',
        inset: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 16,
        border: dragging ? '2px dashed #6c8cff' : '2px dashed #444',
        borderRadius: 12,
        margin: 40,
        transition: 'border-color 0.2s',
        background: dragging ? 'rgba(108, 140, 255, 0.05)' : 'transparent',
      }}
    >
      {loading ? (
        <span style={{ color: '#888', fontSize: 16 }}>Loading...</span>
      ) : (
        <>
          <span style={{ color: '#888', fontSize: 16 }}>
            Drop a folder or files here (.dae / .obj / .fbx + textures)
          </span>
          <span style={{ color: '#555', fontSize: 12 }}>or</span>
          <label
            style={{
              padding: '8px 20px',
              background: '#2a2a4a',
              borderRadius: 6,
              cursor: 'pointer',
              color: '#aaa',
              fontSize: 13,
            }}
          >
            Browse Folder
            {/* @ts-expect-error webkitdirectory is non-standard but widely supported */}
            <input
              type="file"
              webkitdirectory=""
              onChange={handleFileInput}
              style={{ display: 'none' }}
            />
          </label>
        </>
      )}
    </div>
  )
}

/** Recursively collect all files from a DataTransfer, including directory contents. */
async function collectAllFiles(dataTransfer: DataTransfer): Promise<File[]> {
  const entries: FileSystemEntry[] = []
  for (let i = 0; i < dataTransfer.items.length; i++) {
    const entry = dataTransfer.items[i].webkitGetAsEntry?.()
    if (entry) entries.push(entry)
  }

  if (entries.length === 0) {
    // Fallback: no entry API, just use files directly
    return Array.from(dataTransfer.files)
  }

  const files: File[] = []
  await Promise.all(entries.map(e => traverseEntry(e, files)))
  return files
}

async function traverseEntry(entry: FileSystemEntry, out: File[]): Promise<void> {
  if (entry.isFile) {
    const file = await new Promise<File>((resolve, reject) => {
      (entry as FileSystemFileEntry).file(resolve, reject)
    })
    out.push(file)
  } else if (entry.isDirectory) {
    const reader = (entry as FileSystemDirectoryEntry).createReader()
    const entries = await readAllEntries(reader)
    await Promise.all(entries.map(e => traverseEntry(e, out)))
  }
}

function readAllEntries(reader: FileSystemDirectoryReader): Promise<FileSystemEntry[]> {
  return new Promise((resolve) => {
    const all: FileSystemEntry[] = []
    const readBatch = () => {
      reader.readEntries((entries) => {
        if (entries.length === 0) {
          resolve(all)
        } else {
          all.push(...entries)
          readBatch() // readEntries may paginate
        }
      })
    }
    readBatch()
  })
}
