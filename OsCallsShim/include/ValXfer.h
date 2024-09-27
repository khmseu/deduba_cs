#include <cstdint>
#include <cstring>
#include <string>

// Define the namespace
namespace OsCalls
{
    struct TValue;

    typedef bool THandler(TValue* value);

    // Define the THandle struct
    struct THandle
    {
        THandler* handler;
        void* data;
        int64_t index;
    };

    // Define the TType enum
    enum class TType
    {
        None = 0,
        IsNumber,
        IsString,
        IsComplex,
    };

    // Define the TValue struct
    struct TValue
    {
        THandle* Handle;
        TType Type;
        int64_t Number;
        const char* String;
        THandle Complex;
    };

    // Declare the external C functions
    extern "C" {
    bool GetNextValue(TValue* value);
    void CreateHandle(TValue* value, THandler* handler, void* data);
    }
} // namespace OsCalls
