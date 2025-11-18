# DeDuBa Documentation Hub

Welcome to the DeDuBa deduplicating backup system documentation.

## Documentation Sections

### [C# API Reference](xref:DeDuBa)

Browse the complete C# API documentation for DeDuBa, OsCalls, and UtilitiesLibrary. Generated from XML comments via DocFX.

**Key namespaces:**

- [DeDuBa](api/DeDuBa.yml) - Main backup logic
- [OsCalls](api/OsCalls.yml) - Native filesystem interop layer
- [UtilitiesLibrary](api/UtilitiesLibrary.yml) - Shared utilities

### [C++ API Reference (Doxygen)](doxygen/html/index.html)

Native shim library documentation (`libOsCallsShim.so`) exposing POSIX syscalls.

### [Articles](articles/intro.md)

Architecture guides and developer documentation.

---

## Quick Links

- **Getting Started**: See [Introduction](articles/intro.md)
- **Architecture**: Check the [Copilot Instructions](https://github.com/khmseu/deduba_cs/blob/master/.github/copilot-instructions.md) for system overview
- **Build**: Run `dotnet build` (includes C++ shim via Makefile)
 - **Changelog**: [docs/CHANGELOG.md](https://github.com/khmseu/deduba_cs/blob/master/docs/CHANGELOG.md)
