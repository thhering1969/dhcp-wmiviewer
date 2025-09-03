# Repo overview

- **Name**: DhcpWmiViewer
- **Type**: Windows Forms (.NET)
- **Target Framework**: net9.0-windows
- **Output**: WinExe (GUI)
- **Primary entry point**: Program.cs → MainForm

## Build
- Build with `dotnet build` or from Visual Studio.
- Uses Windows Forms: `<UseWindowsForms>true</UseWindowsForms>`.

## Packaging / Deployment
- Three artifacts exist (see README-Deployment.md):
  - **DhcpWmiViewer.exe**: requires .NET 9 runtime
  - **DhcpWmiViewer-Portable.exe**: single-file, self-contained
  - **DhcpWmiViewer-Portable.zip**: portable folder with all runtime files

## Notable dependencies
- Microsoft.PowerShell.SDK (7.5.x)
- System.DirectoryServices (9.0.x)
- System.Management (9.0.x)

## Key folders/files
- MainForm.*.cs: UI setup, handlers, layout, leases/reservations logic
- DhcpManager.*.cs: DHCP operations (query/create/update/delete)
- Helpers: PingHelper, IpUtils, NetworkHelper, PowerShellExecutor
- AdminRightsChecker: elevation checks and UI
- README-Deployment.md: distribution guidance

## Runtime behavior
- Loads config from `%APPDATA%/DhcpWmiViewer/config.txt` (see README)
- Checks admin rights on start and can warn/offer elevation
- Uses WMI/PowerShell and DirectoryServices to query DHCP servers
- Global exception handlers write to temp crash log and optionally EventLog

## Common tasks
- Scopes/Reservations/Leases are displayed in three DataGridViews (top + tabs)
- Context menus for reservations and leases
- CSV export available

## Recent UI tweak (2025-09)
- DataGridView header styles forced to system colors and dynamic height for DPI/theme readability.

## Known caveats
- Admin rights required for many DHCP actions
- Remote DHCP access depends on WMI/WinRM and firewall rules

## How to run
- Debug: Start in Visual Studio or `dotnet run`.
- Release portable: build publish profile for single-file self-contained (see existing artifacts).