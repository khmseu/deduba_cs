# Documentation Build

This repository includes configuration to generate documentation for both the native C++ headers and the C# projects.

## C++ (Doxygen)

Requires `doxygen` installed.

- Config: `docs/Doxyfile`
- Output: `docs/doxygen/html/index.html`

Build:

```bash
cd docs
doxygen Doxyfile
```

## C# (DocFX)

Requires `docfx` available on PATH. The configuration targets the three C# projects and uses the XML documentation produced during `dotnet build`.

- Config: `docs/docfx.json`
- Output site: `docs/_site/index.html`

Generate metadata + site:

```bash
cd docs
# Will read ../DeDuBa/DeDuBa.csproj, ../OsCalls/OsCalls.csproj, ../UtilitiesLibrary/UtilitiesLibrary.csproj
# and use their XML docs from bin/ to generate API YAML, then build the site
# If docfx is not installed, see https://dotnet.github.io/docfx/
docfx docfx.json
```

Tip: Ensure the code is built first so XML docs exist (enabled in the .csproj files).
