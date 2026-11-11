# SQX native bridge

The console includes the original x86/x64 SQX runtime DLLs and bridge from ZipGenius X. Windows x64 operations also require the x86 bridge/runtime because the managed wrapper invokes the 32-bit helper through rundll32. ARM64, Linux and macOS do not support this backend.

The bridge source is included here; `sqx.h` is the original SpeedProject SDK header and retains its copyright. SQX runtime DLLs retain Sven Ritter's copyright and their own terms; they are not relicensed under the console GPL license. Inclusion and redistribution were confirmed by the project maintainer.

To rebuild with Visual Studio C++ and CMake, use separate directories for each architecture:

```powershell
cmake -S native/sqx -B native/sqx/build-x86 -A Win32
cmake --build native/sqx/build-x86 --config Release
cmake -S native/sqx -B native/sqx/build-x64 -A x64
cmake --build native/sqx/build-x64 --config Release
```

Copy each `Release/SqxBridge.dll` to the matching `zgshell/Libs/x86` or `zgshell/Libs/x64` directory.
