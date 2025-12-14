/*
 * LibraryLifecycle.cpp
 * Log library load/unload events for diagnostics.
 */
#include "Platform.h"
#include <cstdio>
// WIN32_LEAN_AND_MEAN and NOMINMAX are defined centrally in Platform.h
#include <fileapi.h>
#include <windows.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH: {
        // Only log if debug env var is set: DEDUBA_DEBUG_NATIVE
        char  env[128] = {0};
        DWORD envlen = GetEnvironmentVariableA("DEDUBA_DEBUG_NATIVE", env, sizeof(env));
        if (envlen > 0) {
            char path[MAX_PATH] = {0};
            if (GetModuleFileNameA(hModule, path, MAX_PATH)) {
                fprintf(stderr, "OsCallsWindowsShimNative: DLL_PROCESS_ATTACH module=%s\n", path);
            } else {
                fprintf(stderr,
                        "OsCallsWindowsShimNative: DLL_PROCESS_ATTACH "
                        "(GetModuleFileName failed err=%lu)\n",
                        GetLastError());
            }
        }
        break;
    }
    case DLL_PROCESS_DETACH: {
        char  env[128] = {0};
        DWORD envlen = GetEnvironmentVariableA("DEDUBA_DEBUG_NATIVE", env, sizeof(env));
        if (envlen > 0)
            fprintf(stderr, "OsCallsWindowsShimNative: DLL_PROCESS_DETACH\n");
    } break;
    }
    return TRUE;
}
