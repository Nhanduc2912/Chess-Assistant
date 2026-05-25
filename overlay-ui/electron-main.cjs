const { app, BrowserWindow, screen, globalShortcut } = require('electron');

let mainWindow;
let isVisible = true;

function createWindow() {
  const primaryDisplay = screen.getPrimaryDisplay();
  const { bounds } = primaryDisplay;

  mainWindow = new BrowserWindow({
    x: bounds.x,
    y: bounds.y,
    width: bounds.width,
    height: bounds.height,
    transparent: true,
    frame: false,
    alwaysOnTop: true,
    hasShadow: false,
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false
    }
  });

  // Tùy chọn để click xuyên qua window
  mainWindow.setIgnoreMouseEvents(true, { forward: true });

  // Ẩn window khỏi thanh Taskbar (tuỳ chọn)
  mainWindow.setSkipTaskbar(true);

  mainWindow.loadURL('http://localhost:5173');

  mainWindow.on('closed', function () {
    mainWindow = null;
  });
}

app.whenReady().then(() => {
  createWindow();

  // Register shortcut to toggle overlay visibility
  globalShortcut.register('CommandOrControl+H', () => {
    if (mainWindow) {
      if (isVisible) {
        mainWindow.hide();
        isVisible = false;
      } else {
        mainWindow.show();
        isVisible = true;
      }
    }
  });

  app.on('activate', function () {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('will-quit', () => {
  globalShortcut.unregisterAll();
});

app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') app.quit();
});