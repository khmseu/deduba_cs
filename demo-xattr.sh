#!/bin/bash
# Demonstration of xattr module functionality

set -e

echo "======================================"
echo "Xattr Module Demonstration"
echo "======================================"
echo

# Create test file if it doesn't exist
TEST_FILE="/tmp/test_xattr_file.txt"
if [[ ! -f ${TEST_FILE} ]]; then
	echo "Creating test file with extended attributes..."
	echo "test content" >"${TEST_FILE}"
	setfattr -n user.test_attr -v "test_value" "${TEST_FILE}"
	setfattr -n user.another_attr -v "another_value" "${TEST_FILE}"
	setfattr -n user.description -v "This is a test file with extended attributes" "${TEST_FILE}"
	echo
fi

echo "Test file: ${TEST_FILE}"
echo
echo "Extended attributes (using getfattr):"
getfattr -d "${TEST_FILE}" 2>/dev/null
echo

echo "======================================"
echo "Running C# Xattr Tests"
echo "======================================"
echo

# Derive project directory from script location
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

export LD_LIBRARY_PATH="${SCRIPT_DIR}/OsCallsCommonShim/bin/Debug/net8.0:${SCRIPT_DIR}/OsCallsLinuxShim/bin/Debug/net8.0:${LD_LIBRARY_PATH}"
cd "${SCRIPT_DIR}"
dotnet test --filter "FullyQualifiedName~XattrTests" --logger "console;verbosity=normal"

echo
echo "======================================"
echo "Demonstration Complete!"
echo "======================================"
echo
echo "The xattr module successfully:"
echo "  ✓ Lists all extended attribute names using ListXattr()"
echo "  ✓ Retrieves attribute values using GetXattr()"
echo "  ✓ Handles errors properly (non-existent files/attributes)"
echo "  ✓ Works with symlinks (llistxattr doesn't follow them)"
echo "  ✓ Uses the ValXfer mechanism for C#/C++ interop"
echo
