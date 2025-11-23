cmake_minimum_required(VERSION 3.15)
project(OsCallsWindowsShim VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Source files
file(GLOB SOURCES "src/*.cpp")

# Add ValXfer.cpp from OsCallsCommonShim
list(APPEND SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/../OsCallsCommonShim/src/ValXfer.cpp")

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
)

# Windows-specific settings
if(WIN32)
    target_compile_definitions(OsCallsWindowsShim PRIVATE
        _CRT_SECURE_NO_WARNINGS
        UNICODE
        _UNICODE
        NOMINMAX
    )
    
    # Link against Windows libraries
    target_link_libraries(OsCallsWindowsShim PRIVATE
        advapi32  # For security APIs
        shlwapi   # For path APIs
    )
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
