import { JSON_FILE_ACCEPT } from './mapSchema'

interface BrowserFileHandle {
  getFile: () => Promise<File>
}

export const openTextFile = async (): Promise<{ fileName: string; content: string } | null> => {
  if ('showOpenFilePicker' in window) {
    const pickerWindow = window as Window & {
      showOpenFilePicker?: (options: {
        multiple: boolean
        excludeAcceptAllOption: boolean
        types: Array<{ description: string; accept: Record<string, string[]> }>
      }) => Promise<BrowserFileHandle[]>
    }

    try {
      const handles = await pickerWindow.showOpenFilePicker?.({
        multiple: false,
        excludeAcceptAllOption: false,
        types: [{ description: 'JSON files', accept: { 'application/json': ['.json'] } }],
      })

      const handle = handles?.[0]
      if (!handle) {
        return null
      }

      const file = await handle.getFile()
      return {
        fileName: file.name,
        content: await file.text(),
      }
    } catch {
      return null
    }
  }

  return new Promise((resolve) => {
    const input = document.createElement('input')

    input.type = 'file'
    input.accept = JSON_FILE_ACCEPT

    input.addEventListener('change', async () => {
      const file = input.files?.[0]
      if (!file) {
        resolve(null)
        return
      }

      resolve({
        fileName: file.name,
        content: await file.text(),
      })
    })

    input.click()
  })
}
