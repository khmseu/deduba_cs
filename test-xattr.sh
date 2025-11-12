#!/bin/bash
# Test script for extended attributes (xattr) functionality

set -e

echo "=== Testing Xattr Module ==="
echo

# Create a test file
TEST_FILE="/tmp/test_xattr_file.txt"

echo "Creating test file: ${TEST_FILE}"
echo "test content" > "${TEST_FILE}"

echo
echo "Setting extended attributes..."
setfattr -n user.test_attr -v "test_value" "${TEST_FILE}"
setfattr -n user.another_attr -v "another_value" "${TEST_FILE}"
setfattr -n user.description -v "This is a test file with extended attributes" "${TEST_FILE}"

echo "Extended attributes (getfattr -d):"
getfattr -d "${TEST_FILE}" 2>/dev/null

echo
echo "Now run the C# test to verify xattr reading works"
echo "Example C# code:"
echo '  var xattrList = Xattr.ListXattr("/tmp/test_xattr_file.txt");'
echo '  Console.WriteLine(xattrList.ToJsonString());'
echo '  // Output: ["user.test_attr","user.another_attr","user.description"]'
echo
echo '  var value = Xattr.GetXattr("/tmp/test_xattr_file.txt", "user.test_attr");'
echo '  Console.WriteLine(value["value"]);'
echo '  // Output: "test_value"'
echo

# Cleanup
# rm -f "$TEST_FILE"
