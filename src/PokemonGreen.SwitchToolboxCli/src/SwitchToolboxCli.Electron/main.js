const { app, BrowserWindow, ipcMain, dialog, Menu } = require('electron');
const path = require('path');

// Hide native menu bar — we have our own in-app menu
Menu.setApplicationMenu(null);

function createWindow() {
    const win = new BrowserWindow({
        width: 1200,
        height: 800,
        minWidth: 800,
        minHeight: 600,
        backgroundColor: '#1e1e1e',
        titleBarStyle: 'hiddenInset',
        frame: true,
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false,
        },
    });

    // In dev, load from Vite dev server; in prod, load built files
    const isDev = !app.isPackaged;
    if (isDev) {
        win.loadURL('http://localhost:5173');
        win.webContents.openDevTools({ mode: 'bottom' });
    } else {
        win.loadFile(path.join(__dirname, '../SwitchToolboxCli.React/dist/index.html'));
    }

    return win;
}

app.whenReady().then(() => {
    createWindow();

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
});

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') app.quit();
});

// IPC: Browse for folder
ipcMain.handle('browse-folder', async (_event, title = 'Select Folder') => {
    const result = await dialog.showOpenDialog({
        title,
        properties: ['openDirectory'],
    });
    return result.canceled ? null : result.filePaths[0];
});
