# OsCallsWindowsShim

Windows C++ implementation of filesystem operations for DeDuBa backup.

## Prerequisites

### Cross-compilation on Linux (Ubuntu/Debian)

To cross-compile for Windows on Ubuntu 24.04 or Debian-based systems:

```bash
sudo apt update
sudo apt install mingw-w64 ninja-build
```

This provides:

- `x86_64-w64-mingw32-gcc` / `x86_64-w64-mingw32-g++` - MinGW-w64 cross compilers
- `x86_64-w64-mingw32-windres` - Windows resource compiler
- `ninja` - Fast build system (optional but recommended)

### Native build on Windows

This will require Visual Studio or MSVC command-line tools on Windows.

## Building

### Cross-compile for Windows (from Linux)

Using the provided MinGW-w64 toolchain file:

```bash
# Via dotnet build with RuntimeIdentifier
dotnet build OsCallsWindowsShim.csproj -r win-x64

# Or manually with CMake
cmake -S . -B build-win-x64 -G Ninja \
      -DCMAKE_TOOLCHAIN_FILE=cmake/toolchains/mingw-w64-x86_64.cmake
cmake --build build-win-x64
```

The resulting `OsCallsWindowsShim.dll` will be a PE32+ (Windows x64) DLL.

### Using CMake (Recommended)

```bash
mkdir build
cd build
cmake ..
cmake --build . --config Debug
```

The CMakeLists.txt automatically includes the common OsCallsCommonShim headers (ValXfer.h, Common.h).

### Using Visual Studio

Create a new C++ DLL project and:

1. Add all `.cpp` files from `src/` directory
2. Add include directories:
   - `include/`
   - `../OsCallsCommonShim/include/`
3. Link against: `advapi32.lib`, `shlwapi.lib`

## Implementation Status

- [ ] FileSystem.cpp - win_lstat, win_readlink, win_canonicalize_file_name
- [ ] Security.cpp - win_get_sd (SDDL security descriptors)
- [ ] Streams.cpp - win_list_streams, win_read_stream (ADS support)
- [ ] GetNextValue - Windows implementation of ValueT cursor

## Dependencies

- Windows SDK (10.0.19041.0 or later recommended)
- Include OsCallsCommonShim headers for ValueT definitions
