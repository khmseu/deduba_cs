/**
 * LibraryLifecycle.cpp
 * Log library load events for diagnostic purposes on Linux.
 */
#include "OcExport.h"
#include <cstdio>
#include <cstdlib>
#include <dlfcn.h>
#include <sys/types.h>
#include <unistd.h>
static void __attribute__((constructor)) on_load(void) {
  // Only log if DEDUBA_DEBUG_NATIVE is set in the environment
  const char *env = getenv("DEDUBA_DEBUG_NATIVE");
  if (env == nullptr)
    return;
  Dl_info info;
  if (dladdr((void *)on_load, &info) != 0) {
    fprintf(stderr, "OsCallsLinuxShim: loaded: path=%s\n",
            info.dli_fname ? info.dli_fname : "(unknown)");
  } else {
    fprintf(stderr, "OsCallsLinuxShim: loaded (dladdr failed)\n");
  }
}

static void __attribute__((destructor)) on_unload(void) {
  const char *env = getenv("DEDUBA_DEBUG_NATIVE");
  if (env == nullptr)
    return;
  fprintf(stderr, "OsCallsLinuxShim: unloading\n");
}
