/**
 * @file ValXfer.h
 * @brief Native value transfer structures and APIs used by the C# interop
 * layer.
 *
 * The shim exposes a cursor-like interface. Managed code initializes a ValueT
 * instance via CreateHandle and then repeatedly calls GetNextValue to iterate
 * over a sequence of values forming either an array or an object (key/value
 * pairs).
 */
#ifndef VALXFER_H
#define VALXFER_H

#include <cstdint>
#include <ctime>

// Ensure 8-byte struct alignment across Windows and Linux
#ifdef _MSC_VER
#pragma pack(push, 8)
#endif

// Define the namespace
namespace OsCalls {
struct ValueT;

/**
 * @brief Custom timespec-compatible struct with explicit 64-bit fields.
 * 
 * Standard timespec uses `long` for tv_nsec, which is 32-bit on Windows x64
 * but 64-bit on Linux x64. To ensure consistent struct layout for P/Invoke,
 * we define our own struct with explicit int64_t fields.
 */
struct TimeSpec64 {
  int64_t tv_sec;   ///< Seconds since epoch
  int64_t tv_nsec;  ///< Nanoseconds component
};

/**
 * @brief Function pointer type for value iteration handlers.
 *
 * Handler functions are invoked by GetNextValue to populate the ValueT
 * structure with the next value in the sequence. Return false to signal
 * end of iteration.
 *
 * @param value Pointer to ValueT to populate with next value.
 * @return true if more values remain, false to end iteration.
 */
typedef bool HandlerT(ValueT *value);

// Define the THandle struct
/**
 * @brief Iterator state passed between native calls for streaming values.
 */
struct HandleT {
  HandlerT *handler;
  void     *data1;
  void     *data2;
  int64_t   index;
};

// Define the TType enum
/**
 * @brief Discriminator for the currently exposed value type.
 */
enum class TypeT {
  IsOk = 0,
  IsError,
  IsNumber,
  IsString,
  IsComplex,
  IsTimeSpec,
  IsBoolean,
};

// Define the TValue struct
/**
 * @brief Native representation of a value in the iteration stream.
 */
struct ValueT {
  HandleT     Handle;
  const char *Name;
  TypeT       Type;
  TimeSpec64  TimeSpec;  ///< Custom 64-bit timespec (int64_t for both fields)
  int64_t     Number;
  const char *String;
  ValueT     *Complex;
  bool        Boolean;
};

/**
 * @brief Convenience macro for setting ValueT fields based on type.
 *
 * Sets the Type discriminator, Name field, and the corresponding value field
 * in a single statement. The typ parameter should be one of: Number, String,
 * Complex, TimeSpec, Boolean (without the "Is" prefix).
 *
 * @param typ Type suffix (e.g., Number for TypeT::IsNumber).
 * @param name C-string for the Name field.
 * @param val Value to assign to the corresponding field.
 *
 * Example: set_val(Number, "st_ino", inode_value);
 */
#define set_val(typ, name, val)                                                                    \
  do {                                                                                             \
    value->Type = TypeT::Is##typ;                                                                  \
    value->Name = name;                                                                            \
    value->typ = val;                                                                              \
  } while (0)

/**
 * @name Cursor operations
 * Functions exported with C linkage for consumption via P/Invoke.
 * @{
 */
} // namespace OsCalls

extern "C" {
/** Advance the cursor and populate the current ValueT fields. */
DLL_EXPORT bool GetNextValue(OsCalls::ValueT *value);
/** Initialize a cursor with a handler and user data pointers. */
DLL_EXPORT void
CreateHandle(OsCalls::ValueT *value, OsCalls::HandlerT *handler, void *data1, void *data2);
}

namespace OsCalls {
/** @} */
} // namespace OsCalls

// Restore default packing
#ifdef _MSC_VER
#pragma pack(pop)
#endif

#endif // VALXFER_H
