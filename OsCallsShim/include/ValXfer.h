#ifndef VALXFER_H
#define VALXFER_H

#include <cstdint>
#include <ctime>

// Define the namespace
namespace OsCalls
{
    struct ValueT;

    typedef bool HandlerT(ValueT *value);

    // Define the THandle struct
    struct HandleT
    {
        HandlerT *handler;
        void *data1;
        void *data2;
        int64_t index;
    };

    // Define the TType enum
    enum class TypeT
    {
        IsOk = 0,
        IsError,
        IsNumber,
        IsString,
        IsComplex,
        IsTimeSpec,
    };

    // Define the TValue struct
    struct ValueT
    {
        HandleT Handle;
        TypeT Type;
        const char *Name;
        int64_t Number;
        const char *String;
        ValueT *Complex;
        timespec TimeSpec;
    };

    // Declare the external C functions
    extern "C"
    {
        bool GetNextValue(ValueT *value);
        void CreateHandle(ValueT *value, HandlerT *handler, void *data1, void *data2);
    }
} // namespace OsCalls

#endif // VALXFER_H
