CXX = g++
CXXFLAGS = -std=c++17 -Wall -Wextra -fPIC -Iinclude
LDFLAGS = -shared

# Safety: Use explicit paths (conditional assignment to avoid recursion)
OUT_DIR ?= bin/Debug/net8.0
TARGET = $(OUT_DIR)/libOsCallsCommonShim.so
SOURCES = $(wildcard src/*.cpp)
OBJECTS = $(SOURCES:src/%.cpp=obj/%.o)

.PHONY: all clean

all: $(TARGET)

$(TARGET): $(OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) $(LDFLAGS) -o $@ $^

obj/%.o: src/%.cpp
	@mkdir -p obj
	$(CXX) $(CXXFLAGS) -c -o $@ $<

clean:
	rm -rf obj bin
