# Minimal CMake toolchain for cross-compiling Windows DLLs on Linux via mingw-w64
# Usage:
#   cmake -S . -B build-win64 -G Ninja \
#         -DCMAKE_TOOLCHAIN_FILE=${CMAKE_CURRENT_LIST_DIR}/mingw-w64-x86_64.cmake

set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)

# Triplet/toolchain prefix
set(TOOLCHAIN_PREFIX x86_64-w64-mingw32)

# Compilers
set(CMAKE_C_COMPILER   ${TOOLCHAIN_PREFIX}-gcc)
set(CMAKE_CXX_COMPILER ${TOOLCHAIN_PREFIX}-g++)
set(CMAKE_RC_COMPILER  ${TOOLCHAIN_PREFIX}-windres)

# Where headers/libs for the target live
# Default Ubuntu path for mingw-w64 toolchain
set(CMAKE_FIND_ROOT_PATH /usr/${TOOLCHAIN_PREFIX})

# Search behavior: never look for programs in the target root, but only headers/libs
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)

# Prefer shared outputs unless the project overrides
set(BUILD_SHARED_LIBS ON CACHE BOOL "Build shared libraries" FORCE)
