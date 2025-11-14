#include "ValXfer.h"

namespace OsCalls {
extern "C" {
bool GetNextValue(ValueT *value) {
  auto ret = value->Handle.handler(value);
  value->Handle.index++;
  return ret;
};

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
