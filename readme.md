## ioSender - a gcode sender for grblHAL and Grbl controllers

---

### .NET 8 / Avalonia (Windows and Linux)

Cross-platform build: `ioSender.net.sln` at the repository root.

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Windows installer packaging also requires [Inno Setup](https://jrsoftware.org/isinfo.php).

```bash
# Restore and build
dotnet restore ioSender.net.sln
dotnet build ioSender.net.sln -c Release

# Run ioSender (from repo root)
dotnet run --project ioSender/ioSender.csproj

# Run standalone Grbl config host
dotnet run --project GrblConfigApp/GrblConfigApp.csproj

# Publish
./scripts/publish-linux.sh          # self-contained linux-x64 → artifacts/publish/linux-x64
./scripts/package-deb.sh            # build artifacts/iosender_*_amd64.deb (Linux)
pwsh ./scripts/publish-windows.ps1  # win-x64   → artifacts/publish/win-x64
pwsh ./scripts/package-windows-installer.ps1  # build artifacts/ioSender-Setup-*-win-x64.exe (Windows)
```

On Windows, `ioSender` and `GrblConfigApp` use `CNC.Platform.Windows`; on Linux, `CNC.Platform.Linux`.

**Linux install and packaging:** see [docs/LINUX.md](docs/LINUX.md) (`.deb`, serial `dialout`, dependencies).

**Large layout** (formerly ioSender XL): View → Toggle large layout.

**Linux serial:** add your user to `dialout` (`sudo usermod -aG dialout $USER`) and re-login.

**Localization:** CSV files under `Locale/{culture}/`. Copied next to the executable on build. Override folder with `IOSENDER_LOCALE_ROOT`. Run with `-locale de-DE` or set `<Locale>` in app settings.

Legacy WPF (.NET Framework) sources are no longer in this repository; see [docs/LEGACY_REFERENCE.md](docs/LEGACY_REFERENCE.md) for upstream reference.

---

Please check out the [Wiki](https://github.com/terjeio/Grbl-GCode-Sender/wiki) for further details.

8-bit Arduino controllers needs _Toggle DTR_ selected in order to reset the controller on connect. Behaviour may be erratic if not set.

![Toggle DTR](Media/Sender8.png)

#### Edge pre-releases

Edge pre-releases can be [downloaded from here](https://www.io-engineering.com/downloads), they contains changes yet to be incorporated in a main release and might be buggy and even break existing functionality.  
Use with care and please [post feedback](https://github.com/terjeio/ioSender/discussions/436) on any issues encountered!

No prereleases yet for v2.0.48.

#### General

If you want to test ioSender with grblHAL but do not have a board yet you can use the [grblHAL simulator](https://github.com/grblHAL/Simulator).
Build it with the [Web Builder](https://svn.io-engineering.com:8443/?driver=Simulator&board=Windows), unpack the .exe-files in the downloaded .zip somewhere and
open a command window (cmd or PowerShell) in the folder by \<Shift\>+Right clicking in it, select _Open PowerShell window here_ or
_Open command window here_ from the popup menu to open it.
Then find your computers IP address by typing `ipconfig` - the IP address can be found in the report generated.  
Run the simulator by typing `./grblHAL_sim -p 23` - 23 is the default Telnet port number and you may have to change it if a Telnet server is already running on the machine.
Leave the window open.  
Now start ioSender and select the _Network_ tab in the sender connection dialog, change the port number if you run the simulator with a different port,
type in your computers IP address and click _Ok_ to connect.  
You can run gcode programs, jog, access settings etc. but _not_ use gcodes that needs input - e.g. probing.  
The simulator can be stopped by typing \<Ctrl\>+C in the command window or by closing it.

---

Latest release is [2.0.47](https://github.com/terjeio/ioSender/releases/tag/2.0.47), see the [changelog](changelog.md) for details. 

---

Some UI examples:

![Sender](Media/Sender.png)

Main screen.
<br><br>

![3D view](Media/Sender2.png)

3D view of program, with live update of tool marker.
<br><br>

![3D view](Media/Sender2_XL.png)

XL version, German translation.
<br><br>

![Jog flyout](Media/Sender7.png)

Jogging flyout, supports up to 9 axes. The sender also supports keyboard jogging with \<Shift\> \(speed\) and \<Ctrl\> \(distance\) modifiers.
<br><br>

![Easy configuration](Media/Sender3.png)

Advanced grbl configuration with on-screen documentation. UI is dynamically generated from data in a file and/or from the controller.
<br><br>

![Probing options](Media/Sender4.png)

Probing options.
<br><br>

![Easy configuration](Media/Sender5.png)

Lathe mode.
<br><br>

![Easy configuration](Media/Sender6.png)

Conversational programming for Lathe Mode. Threading requires [grblHAL](https://github.com/grblHAL) controller with driver that has spindle sync support.

---
2026-04-29
