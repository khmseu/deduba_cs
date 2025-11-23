CXX         := g++
CXX_FLAGS   := -Wall -Wextra -std=c++17 -ggdb -fPIC -fdiagnostics-color=always

# Safety: Default to bin/ if OUT_DIR not set
BIN         := $(if $(OUT_DIR),$(OUT_DIR),bin)
SRC         := $(if $(PROJECT_DIR),$(PROJECT_DIR),$(CURDIR))/src
INCLUDE     := $(if $(PROJECT_DIR),$(PROJECT_DIR),$(CURDIR))/include
COMMON_INCLUDE := $(if $(PROJECT_DIR),$(PROJECT_DIR),$(CURDIR))/../OsCallsCommonShim/include
COMMON_LIB  := $(if $(PROJECT_DIR),$(PROJECT_DIR),$(CURDIR))/../OsCallsCommonShim/bin/Debug/net8.0
LIB         := $(if $(PROJECT_DIR),$(PROJECT_DIR),$(CURDIR))/lib

LIBRARIES   := -lacl -L$(COMMON_LIB) -lOsCallsCommonShim -Wl,-rpath,'$$ORIGIN'
SHARED_LIB  := libOsCallsLinuxShim.so

all: $(BIN)/$(SHARED_LIB)

$(BIN)/$(SHARED_LIB): $(SRC)/*.cpp
	mkdir -vp $(BIN)
	$(CXX) $(CXX_FLAGS) -I$(INCLUDE) -I$(COMMON_INCLUDE) -L$(LIB) -shared -o $@ $^ $(LIBRARIES)

clean:
	@if [ -z "$(BIN)" ] || [ "$(BIN)" = "/" ]; then echo "ERROR: BIN is empty or root, refusing to clean"; exit 1; fi
	-rm -f $(BIN)/$(SHARED_LIB) $(BIN)/*.o

#