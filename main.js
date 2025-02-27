const { app, BrowserWindow } = require("electron");

let mainWindow;

// This will run once the app is ready
app.whenReady().then(() => {
  // Create a new browser window
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      nodeIntegration: true,  // Allows Node.js features in your renderer process
    },
  });

  // Load the HTML file for the frontend of your app
  mainWindow.loadFile("index.html");

  // Reopen the window when the app is activated (for macOS)
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

// Quit the app when all windows are closed (except macOS)
app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});
