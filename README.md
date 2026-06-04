# C# Simple View

Quick example illustrating how to view false color images derived from Optris Imagers thermal data.

> **NOTE** The example uses Windows Forms. Therefore, it is limited to Windows only.

## Preparations
Generate the configuration for your camera with the help of the SDL command line tool `ir_generate_config`. Then run
`ir_download_calibration` to download the calibration data for your camera.

## Run the example
Open the solution file `SimpleViewCS.sln` in Visual Studio. Right-click on the project `SimpleViewCS` and select build or rebuild.
To run the generated executable you will have to copy the two DLL files in `lib\` folder to the directory containing the EXE files.
This could, for example, be `bin\Debug\net9.0-windows\`. You only need to do this once for every build configuration.

Now you run the application either by double clicking the EXE file or by hitting the Run button in Visual Studio tool bar.

## Notes
- The DLL files in the `lib\` provide the following
  - `irdirectsdk.dll` - the SDK itself.
  - `irdirectsdk_csharp.dll` - the C# bindings of the SDK.
- The `classes\` folder contains the C# proxy classes and functions that mirroring the C++ API.

