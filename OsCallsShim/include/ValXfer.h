/**
 * @file ValXfer.h
 * @brief Native value transfer structures and APIs used by the C# interop layer.
 *
 * The shim exposes a cursor-like interface. Managed code initializes a ValueT instance
 * via CreateHandle and then repeatedly calls GetNextValue to iterate over a sequence
 * of values forming either an array or an object (key/value pairs).
 */
#ifndef VALXFER_H
#define VALXFER_H

#include <cstdint>
#include <ctime>

// Define the namespace
namespace OsCalls
{
    struct ValueT;

    typedef bool HandlerT(ValueT* value);

    // Define the THandle struct
    /**
     * @brief Iterator state passed between native calls for streaming values.
     */
    struct HandleT
    {
        HandlerT* handler;
        void* data1;
        void* data2;
        int64_t index;
    };

    // Define the TType enum
    /**
     * @brief Discriminator for the currently exposed value type.
     */
    enum class TypeT
    {
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
    struct ValueT
    {
        HandleT Handle;
        const char* Name;
        TypeT Type;
        timespec TimeSpec;
        int64_t Number;
        const char* String;
        ValueT* Complex;
        bool Boolean;
    };

    /**
     * @name Cursor operations
     * Functions exported with C linkage for consumption via P/Invoke.
     * @{
     */
    extern "C" {
    /** Advance the cursor and populate the current ValueT fields. */
    bool GetNextValue(ValueT* value);
    /** Initialize a cursor with a handler and user data pointers. */
    void CreateHandle(ValueT* value, HandlerT* handler, void* data1, void* data2);
    }
    /** @} */
} // namespace OsCalls

#endif // VALXFER_H
