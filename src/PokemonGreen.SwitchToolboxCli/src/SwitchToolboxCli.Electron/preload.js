const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronAPI', {
    browseFolder: (title) => ipcRenderer.invoke('browse-folder', title),
});
