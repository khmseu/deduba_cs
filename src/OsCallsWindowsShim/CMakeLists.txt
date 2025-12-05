cmake_minimum_required(VERSION 3.15)
project(OsCallsWindowsShim VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Source files (Windows shim specific only)
file(GLOB SOURCES "src/*.cpp")

# IMPORTANT: Do NOT append ValXfer.cpp here.
# ValXfer.cpp belongs exclusively to OsCallsCommonShim and is built once
# into that shared library. The platform shims (Linux/Windows) must link
# against OsCallsCommonShim instead of compiling ValXfer.cpp again to avoid
# duplicate objects and missing exports.

# Link against common shim (built separately or by parent CMake)
if(NOT TARGET OsCallsCommonShim)
    add_subdirectory(../OsCallsCommonShim ${CMAKE_CURRENT_BINARY_DIR}/_common_build)
endif()

add_library(OsCallsWindowsShim SHARED ${SOURCES})

# Use a distinct runtime name for the native DLL so it does not conflict with
# the managed assembly produced by the C# project which would otherwise
# shadow native exports at runtime (causing EntryPointNotFoundException).
set_target_properties(OsCallsWindowsShim PROPERTIES
    OUTPUT_NAME "OsCallsWindowsShimNative"
)

# Include directories
target_include_directories(OsCallsWindowsShim PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/../OsCallsCommonShim/include
    ${CMAKE_CURRENT_BINARY_DIR}/_common_build/include  # For generated OsCallsCommonShim_export.h
)

target_link_libraries(OsCallsWindowsShim PRIVATE
    OsCallsCommonShim  # Provides ValXfer.cpp exports
)

# Windows-specific settings
if(WIN32)
    target_link_libraries(OsCallsWindowsShim PRIVATE
        advapi32
        shlwapi
    )

    # Ensure target architecture macros exist for Windows SDK headers.
    # CI shows winnt.h failing with "No Target Architecture" under MSVC.
    # MSVC typically sets _M_X64/_M_AMD64/_WIN64 automatically for 64-bit,
    # but we defensively add equivalents when building 64-bit to satisfy SDK.
    if(CMAKE_SIZEOF_VOID_P EQUAL 8)
        target_compile_definitions(OsCallsWindowsShim PRIVATE
            _AMD64_=1
            _AMD64
            _WIN64
            WIN64
        )
    endif()
endif()

# Output directory layout - use generator expression for multi-config (VS)
# Use 'native' subdirectory to avoid PDB conflict with managed .NET assemblies
if(CMAKE_CONFIGURATION_TYPES)
    set(out_dir ${CMAKE_BINARY_DIR}/bin/$<CONFIG>/native)
else()
    set(out_dir ${CMAKE_BINARY_DIR}/bin/native)
endif()

set_target_properties(OsCallsWindowsShim PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${out_dir}
    LIBRARY_OUTPUT_DIRECTORY ${out_dir}
)

# Install rules
install(TARGETS OsCallsWindowsShim
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION bin
)
