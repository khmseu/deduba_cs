cmake_minimum_required(VERSION 3.15)
project(OsCallsCommonShim LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(OsCallsCommonShim SHARED src/ValXfer.cpp)

target_include_directories(OsCallsCommonShim PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_options(OsCallsCommonShim PRIVATE -Wall -Wextra -fPIC)

# Match existing output layout so scripts and LD_LIBRARY_PATH remain valid
set(out_dir ${CMAKE_CURRENT_SOURCE_DIR}/bin/Debug/net8.0)
file(MAKE_DIRECTORY ${out_dir})
set_target_properties(OsCallsCommonShim PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${out_dir}
    LIBRARY_OUTPUT_DIRECTORY ${out_dir}
    ARCHIVE_OUTPUT_DIRECTORY ${out_dir}
)
