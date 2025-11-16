# OsCallsWindowsShim

Windows C++ implementation of filesystem operations for DeDuBa backup.

## Building

This will require Visual Studio or MSVC command-line tools on Windows.

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
