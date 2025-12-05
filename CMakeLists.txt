cmake_minimum_required(VERSION 3.15)
project(DeDuBa_Native VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Build common shim first (dependency for Linux shim)
add_subdirectory(src/OsCallsCommonShim)

# Build Linux shim (links against OsCallsCommonShim)
if(UNIX AND NOT APPLE)
    add_subdirectory(src/OsCallsLinuxShim)
endif()

# Build Windows shim (already has its own CMakeLists)
if(WIN32)
    add_subdirectory(src/OsCallsWindowsShim)
endif()
