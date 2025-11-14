# Extended Attributes (xattr) Module

The xattr module provides functions to read extended attributes from filesystem paths using the ValXfer mechanism.

## API Reference

### OsCalls.Xattr

The `Xattr` class in the `OsCalls` namespace provides two main functions:

#### ListXattr(string path)

Lists all extended attribute names for the specified path (not following symlinks).

**Parameters:**

- `path` - Filesystem path to read xattrs from

**Returns:**

- `JsonArray` containing the names of all extended attributes

**Example:**

```csharp
using OsCalls;

var xattrList = Xattr.ListXattr("/path/to/file");
Console.WriteLine(xattrList.ToJsonString());
// Output: ["user.test_attr","user.another_attr","user.description"]

foreach (var attr in xattrList.AsArray())
{
    Console.WriteLine($"Attribute: {attr}");
}
```

#### GetXattr(string path, string name)

Gets the value of a specific extended attribute (not following symlinks).

**Parameters:**

- `path` - Filesystem path to read xattr from
- `name` - Name of the extended attribute to retrieve

**Returns:**

- `JsonObject` with a "value" field containing the attribute value as a string

**Example:**

```csharp
using OsCalls;

var value = Xattr.GetXattr("/path/to/file", "user.test_attr");
Console.WriteLine(value["value"]);
// Output: "test_value"

// Full JSON output
Console.WriteLine(value.ToJsonString());
// Output: {"value":"test_value"}
```

## Implementation Details

### ValXfer Mechanism

Both `ListXattr` and `GetXattr` use the ValXfer mechanism to transfer data from native C++ code to managed C#:

1. **C# Layer** (`OsCalls/Xattr.cs`):
   - Declares P/Invoke imports to `libOsCallsShim.so`
   - Calls native functions: `llistxattr()` and `lgetxattr()`
   - Uses `ValXfer.ToNode()` to convert native `ValueT*` to `JsonNode`

2. **C++ Layer** (`OsCallsShim/src/Xattr.cpp`):
   - Implements `llistxattr()` and `lgetxattr()` using POSIX APIs
   - Returns data via `ValueT` structures with handler callbacks
   - Uses iterator pattern for streaming attribute lists

### Native POSIX APIs

The C++ implementation uses:

- `llistxattr(path, buffer, size)` - List extended attributes without following symlinks
- `lgetxattr(path, name, buffer, size)` - Get attribute value without following symlinks

Note: The "l" prefix indicates these functions do **not** follow symbolic links.

## Error Handling

Errors are reported through the `Utilities.Error()` function, which:

- Logs errors with caller context (file, line, member name)
- Wraps the native `Win32Exception` in a `System.Exception`
- Preserves the full exception chain via `InnerException`

**Example Error Handling:**

```csharp
try
{
    var value = Xattr.GetXattr("/path/to/file", "user.nonexistent");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    if (ex.InnerException is Win32Exception win32Ex)
    {
        Console.WriteLine($"Native error code: {win32Ex.NativeErrorCode}");
    }
}
```

## Testing

Comprehensive tests are available in `DeDuBa.Test/XattrTests.cs`:

```bash
# Set library path
export LD_LIBRARY_PATH=/path/to/OsCallsShim/bin/Debug/net8.0:$LD_LIBRARY_PATH

# Run xattr tests
dotnet test --filter "FullyQualifiedName~XattrTests"
```

## Requirements

- Linux with extended attribute support
- `libacl1-dev` package (for compilation)
- `attr` package (for testing with `setfattr`/`getfattr`)

## See Also

- [ValXfer.cs](../OsCalls/ValXfer.cs) - Value transfer mechanism
- [FileSystem.cs](../OsCalls/FileSystem.cs) - Similar POSIX API wrappers
- [XattrTests.cs](../DeDuBa.Test/XattrTests.cs) - Comprehensive test examples
