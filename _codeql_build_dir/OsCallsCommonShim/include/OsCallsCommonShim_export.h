
#ifndef OSCALLS_COMMON_SHIM_EXPORT_H
#define OSCALLS_COMMON_SHIM_EXPORT_H

#ifdef OSCALLS_COMMON_SHIM_STATIC_DEFINE
#  define OSCALLS_COMMON_SHIM_EXPORT
#  define OSCALLS_COMMON_SHIM_NO_EXPORT
#else
#  ifndef OSCALLS_COMMON_SHIM_EXPORT
#    ifdef OsCallsCommonShim_EXPORTS
        /* We are building this library */
#      define OSCALLS_COMMON_SHIM_EXPORT __attribute__((visibility("default")))
#    else
        /* We are using this library */
#      define OSCALLS_COMMON_SHIM_EXPORT __attribute__((visibility("default")))
#    endif
#  endif

#  ifndef OSCALLS_COMMON_SHIM_NO_EXPORT
#    define OSCALLS_COMMON_SHIM_NO_EXPORT __attribute__((visibility("hidden")))
#  endif
#endif

#ifndef OSCALLS_COMMON_SHIM_DEPRECATED
#  define OSCALLS_COMMON_SHIM_DEPRECATED __attribute__ ((__deprecated__))
#endif

#ifndef OSCALLS_COMMON_SHIM_DEPRECATED_EXPORT
#  define OSCALLS_COMMON_SHIM_DEPRECATED_EXPORT OSCALLS_COMMON_SHIM_EXPORT OSCALLS_COMMON_SHIM_DEPRECATED
#endif

#ifndef OSCALLS_COMMON_SHIM_DEPRECATED_NO_EXPORT
#  define OSCALLS_COMMON_SHIM_DEPRECATED_NO_EXPORT OSCALLS_COMMON_SHIM_NO_EXPORT OSCALLS_COMMON_SHIM_DEPRECATED
#endif

/* NOLINTNEXTLINE(readability-avoid-unconditional-preprocessor-if) */
#if 0 /* DEFINE_NO_DEPRECATED */
#  ifndef OSCALLS_COMMON_SHIM_NO_DEPRECATED
#    define OSCALLS_COMMON_SHIM_NO_DEPRECATED
#  endif
#endif

#endif /* OSCALLS_COMMON_SHIM_EXPORT_H */
