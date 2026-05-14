# OptrisXiLogger

WinForms application for recording Optris Xi 640 thermal camera data at 32 Hz
as 16-bit TIFF files with full embedded metadata. Built with .NET 8 and the
Optris OTC SDK v10.1+.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10/11 x64 | Target platform |
| Visual Studio Community 2022 | With ".NET desktop development" workload |
| .NET 8 SDK | Installed via VS workload |
| Optris OTC SDK ≥ 10.1 | Installer sets `OTC_SDK_DIR` env variable |
| NI-DAQmx driver | Only needed if using hardware trigger |
| Optris Xi 640 | Connected via USB |
| Optris PIX Connect | Installed (required for camera calibration files) |

---

## One-time Setup

### 1. Copy OTC SDK C# Wrapper Classes

After installing the OTC SDK, create a `classes/` folder in the project root and copy:

```
C:\Program Files\Optris\otcsdk\bindings\csharp\classes\*.cs
  →  OptrisXiLogger\classes\
```

The `.csproj` includes all `*.cs` under `classes/` automatically.

### 2. Copy SDK Native DLLs to Output

The project references `$(OTC_SDK_DIR)\bin\otcsdk.dll` and `SoSCorrectionLib.dll`
and copies them to the build output automatically (via `.csproj` Content items).
If `OTC_SDK_DIR` is not set, set it manually in your system environment variables
or edit the `.csproj` paths directly.

### 3. Install NuGet Packages

In the Package Manager Console or terminal:

```powershell
dotnet restore
```

This pulls in:
- `NationalInstruments.DAQmx` — NI-DAQmx managed bindings
- `BitMiracle.LibTiff.NET` — TIFF writing
- `System.Text.Json` — settings persistence

> **NI-DAQmx note:** You must install the NI-DAQmx driver *before* the NuGet
> package. The NuGet package is only the managed wrapper; the native driver
> must be present on the target machine.

### 4. Camera XML Configuration File

The Optris SDK requires a camera-specific XML config file. It is typically found at:

```
C:\Users\<You>\AppData\Roaming\Imager\<serial_number>.xml
```

After connecting the camera and running PIX Connect once, this file is generated
automatically. Point the application to it via **Settings → Camera XML Config**.

---

## Building

1. Open `OptrisXiLogger.sln` (or the `.csproj`) in Visual Studio.
2. Set configuration to **Release | x64**.
3. Build → Build Solution (`Ctrl+Shift+B`).

Output will be in `bin\x64\Release\net8.0-windows\`.

### Self-contained deployment (optional)

To produce a single-folder EXE deployable without .NET installed on the target:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## Usage

### First Run

1. Launch `OptrisXiLogger.exe`.
2. In the **Settings** panel (right side), set:
   - **Save Directory** — where TIFF sessions will be written
   - **Camera XML Config** — path to the camera XML file (see above)
   - **Emissivity / Ambient Temp** — match your measurement conditions
3. Click **Connect Camera**.
4. Adjust **Color Palette**, **Display FPS**, and temperature range as desired.
5. Click **Apply Settings**.

### Manual Recording

- Click **⏺ Record** to start. A timestamped session folder is created immediately.
- Click **⏹ Stop** to end the session. The status bar shows frames saved and dropped.

### Triggered Recording (NI-DAQ)

1. In Settings, enable **External Trigger**, set **DAQ Device** (e.g. `Dev1`),
   **Digital Line** (e.g. `port0/line0`), and **Trigger Edge**.
2. Click **Apply Settings**.
3. Click **⚡ Arm Trigger**. The application waits for the hardware edge.
4. On the rising (or falling) edge, recording starts automatically.
5. Click **⏹ Stop** or disarm the trigger to end the session.

---

## TIFF File Format

Each frame is saved as a separate 16-bit grayscale TIFF:

- **Filename:** `frame_<index8d>_<YYYYMMDD_HHmmss_fff>.tiff`
- **Pixel encoding:** `Temperature (°C) = (pixel_value / 10.0) - 100.0`
- **Byte order:** little-endian
- **Compression:** none (raw)
- **Metadata:** embedded in TIFF `ImageDescription` tag as XML, containing:
  - UTC timestamp (ISO 8601, sub-millisecond)
  - Frame index and SDK timestamp
  - Min / Max / Average temperature (°C)
  - Hotspot pixel coordinates (X, Y)
  - Emissivity, ambient temp, transmitted temp
  - Shutter flag state
  - Software name and version
  - Pixel encoding formula

### Reading TIFFs in Python

```python
import tifffile
import numpy as np

img = tifffile.imread("frame_00000001_20250101_120000_000.tiff")
temp_celsius = img / 10.0 - 100.0
print(f"Max: {temp_celsius.max():.2f} °C")
```

---

## Architecture Notes

| Layer | Component | Role |
|---|---|---|
| Camera | `ThermalCameraClient` | OTC SDK IRImagerClient subclass; 32 Hz callback |
| Buffer | `FrameBuffer` | 256-slot ring buffer; decouples camera from disk |
| Disk | `TiffWriter` | 16-bit TIFF with embedded XML metadata |
| Trigger | `NiDaqTrigger` | ~1 kHz digital edge poll via NI-DAQmx |
| Control | `AcquisitionController` | Orchestrates recording state and writer thread |
| UI | `MainForm` | WinForms host; display timer throttled to `DisplayFps` |
| Display | `ThermalDisplayPanel` | False-color render, hotspot crosshair, colorbar |
| Settings | `SettingsPanel` | All user-configurable parameters; persisted to JSON |

**Thread model:**

```
Camera grab thread (highest priority)
  └─ onThermalFrame() → FrameBuffer.Enqueue()

TIFF writer thread (above normal priority)
  └─ FrameBuffer.Dequeue() → TiffWriter.Write()

UI thread
  └─ DisplayTimer.Tick() → FrameBuffer.PeekLatest() → ThermalDisplayPanel.UpdateFrame()
```

The camera callback thread is never blocked by disk I/O. If the writer falls
behind (slow disk), frames are dropped from the ring buffer tail and counted in
the `Dropped` status bar indicator.

---

## Troubleshooting

| Symptom | Likely Cause |
|---|---|
| `IRImager.init()` returns 0 | Wrong XML path; camera not recognized; run PIX Connect first |
| No display update | Camera not connected; check status bar |
| High dropped frame count | Disk too slow; use an SSD, reduce fragmentation |
| NI-DAQmx arm fails | Driver not installed; wrong device/line name; check NI MAX |
| TIFF write errors | Disk full; permissions issue on save directory |
