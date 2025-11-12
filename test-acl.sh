#!/bin/bash
# Test script for ACL functionality

set -e

echo "=== Testing ACL Module ==="
echo

# Create a test file
TEST_FILE="/tmp/test_acl_file.txt"
TEST_DIR="/tmp/test_acl_dir"

echo "Creating test file: ${TEST_FILE}"
touch "${TEST_FILE}"
chmod 644 "${TEST_FILE}"

echo "Creating test directory: ${TEST_DIR}"
mkdir -p "${TEST_DIR}"
chmod 755 "${TEST_DIR}"

echo
echo "Setting access ACL on file..."
setfacl -m u:daemon:rwx "${TEST_FILE}"
echo "Access ACL (getfacl):"
getfacl -c "${TEST_FILE}" 2>/dev/null

echo
echo "Setting default ACL on directory..."
setfacl -d -m u:daemon:rwx "${TEST_DIR}"
echo "Default ACL (getfacl):"
getfacl -d "${TEST_DIR}" 2>/dev/null | grep -v "^#"

echo
echo "Now run the C# test to verify ACL reading works"
echo "Example C# code:"
echo '  var accessAcl = Acl.GetFileAccess("/tmp/test_acl_file.txt");'
echo '  Console.WriteLine(accessAcl["acl_text"]);'
echo
echo '  var defaultAcl = Acl.GetFileDefault("/tmp/test_acl_dir");'
echo '  Console.WriteLine(defaultAcl["acl_text"]);'
echo

# Cleanup
# rm -f "$TEST_FILE"
# rm -rf "$TEST_DIR"
