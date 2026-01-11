# Quake II Server Browser (Q2Pro Edition)

A high-performance, native Windows desktop application to browse Quake II multiplayer servers. Built with .NET 8 and WPF.

## Prerequisites

- **.NET 8 SDK** - Download from [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Windows 10/11** (x64)
- **Visual Studio Code** with the **C# extension** (ms-dotnettools.csharp)

## Building the Project

### Using Visual Studio Code

1. **Open the project folder** in Visual Studio Code:
   ```bash
   code .
   ```

2. **Install the C# extension** (if not already installed):
   - Press `Ctrl+Shift+X` to open Extensions
   - Search for "C#" by Microsoft
   - Click Install

3. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

4. **Build the solution**:
   ```bash
   dotnet build
   ```

   Or build a specific project:
   ```bash
   dotnet build Q2Browser.Wpf/Q2Browser.Wpf.csproj
   ```

5. **Run the application**:
   ```bash
   dotnet run --project Q2Browser.Wpf/Q2Browser.Wpf.csproj
   ```

### Alternative: Build from Terminal

Open PowerShell or Command Prompt in the project root and run:

```powershell
# Restore NuGet packages
dotnet restore

# Build the entire solution
dotnet build Q2Browser.sln

# Run the WPF application
dotnet run --project Q2Browser.Wpf/Q2Browser.Wpf.csproj
```

## Configuration

Before launching games, you need to configure the Q2Pro executable path and other settings.

### Using the Settings Window

1. **Launch the application** (see [Running the Application](#running-the-application) below)
2. **Click the "Settings" button** in the main window toolbar
3. **Configure your settings**:
   - **Q2Pro Executable**: Click "Browse..." to select your `q2pro.exe` file
   - **Master Server**: Configure HTTP or UDP master server settings
   - **Options**: Enable/disable refresh on startup, LAN broadcast discovery
   - **Advanced**: Adjust concurrent probes and timeout settings
4. **Click "Save"** to apply your changes

### Settings File Location

Settings are stored in:
```
%AppData%\Q2ServerBrowser\settings.json
```

**Note**: While you can manually edit this JSON file if needed, it's recommended to use the Settings window in the application UI to avoid configuration errors.

## Running the Application

1. **Launch the app**:
   ```bash
   dotnet run --project Q2Browser.Wpf/Q2Browser.Wpf.csproj
   ```

2. **Click "Refresh"** to query the master server and discover game servers

3. **Search** for servers by name, map, or mod using the search box

4. **Double-click a server** or select it and click "Connect" to launch Q2Pro

5. **Toggle favorites** by selecting a server and clicking "Toggle Favorite"

## Project Structure

```
Q2Browser.sln
├── Q2Browser.Core/          # Core library (no WPF dependencies)
│   ├── Models/              # ServerEntry, Settings
│   ├── Networking/          # MasterServerClient, GameServerProbe
│   ├── Protocol/           # PacketHeader, Q2ColorParser, ByteReader
│   └── Services/           # FavoritesService
│
└── Q2Browser.Wpf/          # WPF application
    ├── ViewModels/         # MainViewModel, ServerRowViewModel
    ├── Views/             # MainWindow.xaml
    ├── Converters/        # Q2ColorToBrushConverter
    └── Services/          # LauncherService
```

## Features

- ✅ Query Quake II master servers
- ✅ Probe individual game servers with throttled concurrent requests
- ✅ Real-time server list updates (non-blocking UI)
- ✅ Search and filter servers
- ✅ Favorites persistence
- ✅ Direct launch Q2Pro with server connection
- ✅ Quake II color code support (^1, ^2, etc.)
- ✅ Virtualized UI for smooth scrolling through hundreds of servers

## Troubleshooting

### Build Errors

- **Missing .NET 8 SDK**: Ensure you have .NET 8 SDK installed (`dotnet --version` should show 8.x)
- **NuGet restore fails**: Try `dotnet nuget locals all --clear` then `dotnet restore`

### Runtime Issues

- **Q2Pro not launching**: Check that `Q2ProExecutablePath` in settings.json points to a valid executable
- **No servers found**: Check your internet connection and firewall settings (UDP port 27900)
- **UI freezes during refresh**: This shouldn't happen due to throttled updates, but if it does, reduce `MaxConcurrentProbes` in settings

## Development Notes

- The Core library (`Q2Browser.Core`) has no WPF dependencies, making it portable for future cross-platform migrations
- Server probing is throttled to 75 concurrent requests by default to prevent router packet loss
- UI updates are batched every 150ms to prevent thread saturation
- All networking operations are fully async and non-blocking

## License

This project is provided as-is for educational and personal use.
