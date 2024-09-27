#include "ValXfer.h"

namespace OsCalls {

bool GetNextValue(TValue *value) {
  auto ret = value->Handle->handler(value);
  value->Handle->index++;
  return ret;
};

void CreateHandle(TValue *value, THandler *handler, void *data) {
  value->Handle->handler = handler;
  value->Handle->data = data;
  value->Handle->index = 0;
};

} // namespace OsCalls