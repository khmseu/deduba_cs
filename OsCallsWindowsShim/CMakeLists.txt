cmake_minimum_required(VERSION 3.15)
project(OsCallsWindowsShim VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Source files
file(GLOB SOURCES "src/*.cpp")

# Add ValXfer.cpp from OsCallsCommonShim
list(APPEND SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../OsCallsCommonShim/src/ValXfer.cpp")

# Link against common shim (built separately or by parent CMake)
if(NOT TARGET OsCallsCommonShim)
    add_subdirectory(../OsCallsCommonShim ${CMAKE_CURRENT_BINARY_DIR}/_common_build)
endif()

# Create shared library (DLL on Windows)
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

# Windows-specific settings
if(WIN32)
    # Windows compile-time defines moved into Platform.h for centralization

    # Link against Windows libraries
    target_link_libraries(OsCallsWindowsShim PRIVATE
        advapi32  # For security APIs
        shlwapi   # For path APIs
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

# Set output directory
set_target_properties(OsCallsWindowsShim PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

# Install rules
install(TARGETS OsCallsWindowsShim
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION bin
)
