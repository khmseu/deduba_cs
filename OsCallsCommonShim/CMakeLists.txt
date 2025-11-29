cmake_minimum_required(VERSION 3.15)
project(OsCallsCommonShim LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(OsCallsCommonShim SHARED src/ValXfer.cpp)

target_include_directories(OsCallsCommonShim PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# Compiler-specific warning / codegen flags
if(MSVC)
    # Use warning level 4; GCC/Clang flags like -Wextra trigger D8021 on cl.exe
    target_compile_options(OsCallsCommonShim PRIVATE /W4)
else()
    target_compile_options(OsCallsCommonShim PRIVATE -Wall -Wextra -fPIC)
endif()

# Ensure consistent feature-test macros for Linux builds (also fallback in Platform.h)
if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    target_compile_definitions(OsCallsCommonShim PRIVATE _GNU_SOURCE _DEFAULT_SOURCE _ATFILE_SOURCE="1" _FILE_OFFSET_BITS=64)
endif()

# Match existing output layout per build type (Debug/Release)
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Debug)
endif()
string(TOLOWER "${CMAKE_BUILD_TYPE}" _lower_bt)
set(out_dir ${CMAKE_CURRENT_SOURCE_DIR}/bin/${CMAKE_BUILD_TYPE}/net8.0)
file(MAKE_DIRECTORY ${out_dir})
set_target_properties(OsCallsCommonShim PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${out_dir}
    LIBRARY_OUTPUT_DIRECTORY ${out_dir}
    ARCHIVE_OUTPUT_DIRECTORY ${out_dir}
)
