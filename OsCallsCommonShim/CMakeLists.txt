cmake_minimum_required(VERSION 3.15)
project(OsCallsCommonShim LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Include module for generating export headers
include(GenerateExportHeader)

add_library(OsCallsCommonShim SHARED src/ValXfer.cpp)

# Generate export header with proper __declspec(dllexport) macros
generate_export_header(OsCallsCommonShim
    BASE_NAME OSCALLS_COMMON_SHIM
    EXPORT_FILE_NAME ${CMAKE_CURRENT_BINARY_DIR}/include/OsCallsCommonShim_export.h
)

target_include_directories(OsCallsCommonShim PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_BINARY_DIR}/include
)

# Ensure all symbols are exported for MSVC (helps with P/Invoke)
set_target_properties(OsCallsCommonShim PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)

# Compiler-specific warning / codegen flags
if(MSVC)
    # Use warning level 4; GCC/Clang flags like -Wextra trigger D8021 on cl.exe
    target_compile_options(OsCallsCommonShim PRIVATE /W4)
    # Ensure exact export names via module definition file
    target_sources(OsCallsCommonShim PRIVATE src/exports.def)
    # Post-build: dump exports for debugging
    add_custom_command(TARGET OsCallsCommonShim POST_BUILD
        COMMAND dumpbin /EXPORTS $<TARGET_FILE:OsCallsCommonShim> > $<TARGET_FILE_DIR:OsCallsCommonShim>/exports.txt
        VERBATIM)
else()
    target_compile_options(OsCallsCommonShim PRIVATE -Wall -Wextra -fPIC)
    # For MinGW cross-compile, pass .def file to linker
    if(WIN32)
        target_link_options(OsCallsCommonShim PRIVATE "-Wl,--output-def,${CMAKE_CURRENT_SOURCE_DIR}/src/exports.def")
        set_target_properties(OsCallsCommonShim PROPERTIES LINK_FLAGS "${CMAKE_CURRENT_SOURCE_DIR}/src/exports.def")
    endif()
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
