#include "OcExport.h"
#include "ValXfer.h"

namespace OsCalls {
extern "C" {
/**
 * @brief Advances the cursor to the next value in the iteration stream.
 *
 * Calls the handler function stored in the ValueT's Handle and increments
 * the index counter. The handler populates the ValueT fields with the next
 * value's metadata and data.
 *
 * @param value Pointer to ValueT structure with initialized Handle.
 * @return true if more values remain, false if iteration is complete.
 */
bool GetNextValue(ValueT *value) {
  auto ret = value->Handle.handler(value);
  value->Handle.index++;
  return ret;
};

/**
 * @brief Initializes a ValueT cursor with a handler function and user data.
 *
 * Sets up the iteration state for a new sequence of values. The handler
 * will be invoked by GetNextValue to populate each value in turn.
 *
 * @param value Pointer to ValueT to initialize.
 * @param handler Function pointer that yields values sequentially.
 * @param data1 First user data pointer (passed to handler via Handle.data1).
 * @param data2 Second user data pointer (passed to handler via Handle.data2).
 */
void CreateHandle(ValueT *value, HandlerT *handler, void *data1, void *data2) {
  value->Handle.handler = handler;
  value->Handle.data1 = data1;
  value->Handle.data2 = data2;
  value->Handle.index = 0;
  value->Type = TypeT::IsError;
  value->Name = "errno";
  value->Number = 0;
  value->String = nullptr;
  value->Complex = nullptr;
  value->TimeSpec = {0, 0};
};
}
} // namespace OsCalls
