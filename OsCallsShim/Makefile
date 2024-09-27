CXX         := g++
CXX_FLAGS   := -Wall -Wextra -std=c++17 -ggdb -fPIC -fdiagnostics-color=always

BIN         := bin
SRC         := src
INCLUDE     := include
LIB         := lib

LIBRARIES   :=
SHARED_LIB  := libOsCallsShim.so

all: $(BIN)/$(SHARED_LIB)

$(BIN)/$(SHARED_LIB): $(SRC)/*.cpp
	mkdir -vp $(BIN)
	$(CXX) $(CXX_FLAGS) -I$(INCLUDE) -L$(LIB) -shared -o $@ $^

clean:
	-rm $(BIN)/* *.o

#