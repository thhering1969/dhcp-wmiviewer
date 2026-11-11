**English** | [Deutsch](README.de.md)

# 🌐 DHCP WMI Viewer

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![OS](https://img.shields.io/badge/OS-Windows-blue.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/thhering1969/dhcp-wmiviewer)](https://github.com/thhering1969/dhcp-wmiviewer/releases/latest)

A Windows desktop tool for everyday work on **Windows DHCP servers**: view scopes, reservations and
leases, turn leases into reservations, and change or delete reservations – with a confirmation before
every change and automatic **failover replication** to the partner server.

> The user interface is available in English and German. It follows the Windows display language;
> *Extras → Sprache / Language* overrides it.

## Highlights

- **Server discovery from Active Directory** – shows only DHCP servers that still exist and are reachable
  (computer account active, WinRM port reachable); decommissioned servers are hidden
- **Confirmation before every change** – a before/after table of all fields, changed values highlighted;
  "Cancel" is the default
- **Automatic failover replication** – after every change the scope is replicated to the failover partner,
  like *Replicate Scope* in the DHCP console (`Invoke-DhcpServerv4FailoverReplication`); also available
  manually via right-click on a scope
- **Audit trail** – every change and replication is written to the Application event log of **both**
  failover partners
- **RSAT first, WinRM as fallback** – uses the local RSAT DHCP tools when installed, otherwise PowerShell
  remoting on the DHCP server; the window title shows which one is used
- **IP picker** – free addresses of the scope with ping/DNS status; optionally marks addresses that are
  allowed to reach the internet (e.g. defined by your firewall)
- **Events from all DHCP servers** – the app's own change log, merged from all active servers
- **Portable** – a single self-contained EXE, no installation, no .NET runtime required

## Download

Get `DhcpWmiViewer-Portable.exe` from the [latest release](https://github.com/thhering1969/dhcp-wmiviewer/releases/latest).

| File | Size | Notes |
|---|---|---|
| `DhcpWmiViewer-Portable.exe` ⭐ | ~153 MB | Single file, self-contained – just run it |
| `DhcpWmiViewer-Portable.zip` | ~64 MB | Same app as a folder; unzip and run `DhcpWmiViewer.exe` |

The app asks to restart with administrator rights if it was not started elevated.

## Requirements

- Windows 10/11 or Windows Server, x64, domain member (for discovery)
- An account with DHCP administration rights (e.g. member of *DHCP Administrators*)
- Access to the DHCP servers via **one** of:
  - **RSAT DHCP tools** on the PC (recommended; required for failover replication) – the app offers to
    install them when needed (`Add-WindowsCapability -Online -Name Rsat.DHCP.Tools~~~~0.0.1.0`), or
  - **WinRM** (PowerShell remoting, TCP 5985) on the DHCP servers
- Reading/writing the event log always uses WinRM

## Usage

1. Start the app, enter a DHCP server or click **Discover** and pick one; press **Query Scopes** (or Enter)
2. Select a scope – reservations and leases load in the tabs below
3. **Leases** tab: right-click → *Create reservation from lease…* (with IP picker)
4. **Reservations** tab: right-click → *Edit…* (name, description, IP) or *Delete*;
   double-click also opens the editor
5. **Events** tab: changes made with this app, from all active DHCP servers
6. Scope table: right-click → *Replicate scope…* manually
7. **Export CSV** saves the scope list

The last queried server is remembered.

### Why RSAT for replication?

Run over WinRM, `Invoke-DhcpServerv4FailoverReplication` fails because of the Kerberos *double hop*:
the DHCP server is not allowed to pass your credentials on to its partner. With the RSAT tools the
command runs on your PC and talks to both servers directly – exactly like the DHCP console.

## Configuration

Settings are stored in `%APPDATA%\DhcpWmiViewer\config.txt`:

```ini
EnableAutoRestartAsAdmin=true
ShowAdminWarning=true
# set automatically after a successful query
LastServer=dhcp01.example.com
# optional: de or en (empty = Windows display language)
Language=en
# optional, see below (empty = feature off)
InternetRanges=10.0.0.10-10.0.0.50, 10.0.0.200-10.0.0.254
```

**Internet ranges (optional):** if only certain addresses may reach the internet (e.g. defined by your
firewall), enter them under *Extras → Internet ranges (firewall)…*. The IP picker then marks them
with 🌐 and can filter for them, and the confirmation shows "with/without internet access".
Without entries the feature is hidden.

**Defaults for many PCs:** place a `DhcpWmiViewer.config.txt` (same format) next to the EXE, e.g. on a
network share. User settings take precedence.

*Extras → Show PowerShell commands before running* shows every command before it runs (debug).

## Troubleshooting

| Problem | What to check |
|---|---|
| Discover finds no servers | PC must be a domain member; DHCP servers must be authorized in AD |
| Server hidden by Discover | Computer account disabled/missing or WinRM port 5985 not reachable (VPN?) |
| "Access denied" | Account needs DHCP administration rights; *Clear Credentials* and enter another account |
| Replication fails | Install the RSAT DHCP tools; the scope needs a failover relationship |
| RSAT installation hangs/fails | Windows Update / WSUS must provide optional features (*Features on Demand*) |

Log files in `%TEMP%`: `DhcpWmiViewer-error.log`, `DhcpWmiViewer-crash.log`,
`DhcpWmiViewer-eventlog-fallback.log`, `dhcp_debug_log.txt`.

## Building

Requires the .NET 9 SDK.

```powershell
git clone https://github.com/thhering1969/dhcp-wmiviewer.git
cd dhcp-wmiviewer
dotnet build
# portable single-file EXE
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Every version tag (`v*`) triggers [`.github/workflows/release.yml`](.github/workflows/release.yml),
which builds the portable EXE and ZIP and publishes a GitHub release.

### Project structure

| File | Purpose |
|---|---|
| `MainForm*.cs` | Main window: layout, scopes, reservations, leases, events, logging/replication |
| `DhcpManager*.cs` | DHCP queries and changes, failover replication, RSAT installation |
| `PowerShellExecutor.cs` | Runs commands: local RSAT first, otherwise WinRM (connections reused) |
| `DhcpDiscovery.cs` | AD discovery including reachability check |
| `ConfirmChangeDialog.cs` | Before/after confirmation |
| `IpPickerForm.cs`, `IpRanges.cs` | IP picker, scope address space, internet ranges |
| `AppConfig.cs`, `InternetRangesDialog.cs` | Settings |
| `Loc.cs` | German/English UI texts (`Loc.T("Deutsch", "English")`) |
| `Theme.cs`, `WaitDialog.cs` | Look & feel, progress dialog |

## Contributing

Issues and pull requests are welcome – see [CONTRIBUTING.md](CONTRIBUTING.md).
Please report bugs via [GitHub Issues](https://github.com/thhering1969/dhcp-wmiviewer/issues) and
leave out internal server names and addresses.

## License

[MIT](LICENSE)
