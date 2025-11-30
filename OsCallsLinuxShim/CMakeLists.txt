cmake_minimum_required(VERSION 3.15)
project(OsCallsLinuxShim LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

file(GLOB SRC_FILES "src/*.cpp")

# Link against common shim (built separately or by parent CMake)
if(NOT TARGET OsCallsCommonShim)
    add_subdirectory(../OsCallsCommonShim ${CMAKE_CURRENT_BINARY_DIR}/_common_build)
endif()

add_library(OsCallsLinuxShim SHARED ${SRC_FILES})

target_include_directories(OsCallsLinuxShim PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/../OsCallsCommonShim/include
    ${CMAKE_CURRENT_BINARY_DIR}/_common_build/include  # For generated OsCallsCommonShim_export.h
)

target_compile_options(OsCallsLinuxShim PRIVATE -Wall -Wextra -fPIC -ggdb)

# Ensure consistent feature-test macros for Linux builds (also fallback in Platform.h)
if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    target_compile_definitions(OsCallsLinuxShim PRIVATE _GNU_SOURCE _DEFAULT_SOURCE _ATFILE_SOURCE="1" _FILE_OFFSET_BITS=64)
endif()

find_library(ACL_LIB acl REQUIRED)
target_link_libraries(OsCallsLinuxShim PRIVATE OsCallsCommonShim ${ACL_LIB})
target_link_options(OsCallsLinuxShim PRIVATE "-Wl,-rpath,'$ORIGIN'")

# Output directory layout for both single-config and multi-config generators
if(CMAKE_CONFIGURATION_TYPES)
    # Multi-config generator (Visual Studio, Xcode)
    set(out_dir ${CMAKE_CURRENT_SOURCE_DIR}/bin/$<CONFIG>/net8.0)
else()
    # Single-config generator (Unix Makefiles, Ninja)
    if(NOT CMAKE_BUILD_TYPE)
        set(CMAKE_BUILD_TYPE Debug)
    endif()
    set(out_dir ${CMAKE_CURRENT_SOURCE_DIR}/bin/${CMAKE_BUILD_TYPE}/net8.0)
    file(MAKE_DIRECTORY ${out_dir})
endif()

set(CMAKE_BUILD_RPATH "$ORIGIN")
set_target_properties(OsCallsLinuxShim PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${out_dir}
    LIBRARY_OUTPUT_DIRECTORY ${out_dir}
    ARCHIVE_OUTPUT_DIRECTORY ${out_dir}
    BUILD_RPATH "$ORIGIN"
    INSTALL_RPATH "$ORIGIN"
)
