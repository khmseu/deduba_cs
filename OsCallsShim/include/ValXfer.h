#ifndef VALXFER_H
#define VALXFER_H

#include <cstdint>
#include <cstring>

// Define the namespace
namespace OsCalls
{
    struct ValueT;

    typedef bool HandlerT(ValueT* value);

    // Define the THandle struct
    struct HandleT
    {
        HandlerT* handler;
        void* data1;
        void* data2;
        int64_t index;
    };

    // Define the TType enum
    enum class TypeT
    {
        IsOK = 0,
        IsError,
        IsNumber,
        IsString,
        IsComplex,
    };

    // Define the TValue struct
    struct ValueT
    {
        HandleT Handle;
        TypeT Type;
        const char* Name;
        int64_t Number;
        const char* String;
        ValueT* Complex;
    };

    // Declare the external C functions
    extern "C" {
    bool GetNextValue(ValueT* value);
    void CreateHandle(ValueT* value, HandlerT* handler, void* data1, void* data2);
    }
} // namespace OsCalls

#endif // VALXFER_H
