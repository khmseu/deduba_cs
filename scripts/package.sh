#!/usr/bin/env bash
set -euo pipefail

# Package DeDuBa into distributable archives for linux-x64 and/or win-x64.
# Produces self-contained single-file builds plus required native shims.
#
# Usage:
#   scripts/package.sh [linux-x64|win-x64|all] [Debug|Release]
#
# Outputs (version from MinVer):
#   dist/DeDuBa-<version>-<rid>/ ... files ...
#   dist/DeDuBa-<version>-<rid>.tar.gz (Linux)
#   dist/DeDuBa-<version>-<rid>.zip    (Windows)

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
DIST_DIR="${ROOT_DIR}/dist"
CONFIG="${2:-Release}"
TARGET="${1:-all}"

mkdir -p "${DIST_DIR}"

log() { echo "[package] $*"; }

get_version() {
  # Prefer msbuild property if available; fallback to parsing build output.
  if VERSION_LINE=$(dotnet msbuild "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -nologo -t:MinVer -getProperty:MinVerVersion 2>/dev/null); then
    [[ -n "${VERSION_LINE}" ]] && echo "${VERSION_LINE}" && return 0
  fi
  dotnet build "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -c "${CONFIG}" -nologo | \
    awk '/MinVer: Calculated version/{print $4; exit}' || echo "0.0.0-unknown"
}

VERSION="$(get_version)"
log "Resolved version: ${VERSION}"

publish_linux() {
  local rid="linux-x64"
  local base_name="DeDuBa-${VERSION}-${rid}"
  local out_dir="${DIST_DIR}/${base_name}"
  log "Building native shims for ${rid} (${CONFIG})"
  dotnet build "${ROOT_DIR}/OsCallsCommonShim/OsCallsCommonShim.csproj" -c "${CONFIG}" >/dev/null
  dotnet build "${ROOT_DIR}/OsCallsLinuxShim/OsCallsLinuxShim.csproj" -c "${CONFIG}" >/dev/null

  log "Publishing managed app for ${rid} (${CONFIG})"
  dotnet publish "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -c "${CONFIG}" -r "${rid}" \
    -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${out_dir}" >/dev/null

  # Copy native shims next to the binary
  local shim_common="${ROOT_DIR}/OsCallsCommonShim/bin/${CONFIG}/net8.0/libOsCallsCommonShim.so"
  local shim_linux="${ROOT_DIR}/OsCallsLinuxShim/bin/${CONFIG}/net8.0/libOsCallsLinuxShim.so"
  if [[ -f "${shim_common}" ]]; then cp -f "${shim_common}" "${out_dir}/"; else log "WARN: missing ${shim_common}"; fi
  if [[ -f "${shim_linux}" ]]; then cp -f "${shim_linux}" "${out_dir}/"; else log "WARN: missing ${shim_linux}"; fi

  # Add a runnable wrapper to set LD_LIBRARY_PATH so the shims resolve
  cat >"${out_dir}/run.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
export LD_LIBRARY_PATH="${SCRIPT_DIR}:${LD_LIBRARY_PATH:-}"
exec "${SCRIPT_DIR}/DeDuBa" "$@"
EOF
  chmod +x "${out_dir}/run.sh"

  # Archive
  (cd "${DIST_DIR}" && tar czf "${base_name}.tar.gz" "$(basename "${out_dir}")")
  log "Created ${DIST_DIR}/${base_name}.tar.gz"
}

publish_windows() {
  local rid="win-x64"
  local base_name="DeDuBa-${VERSION}-${rid}"
  local out_dir="${DIST_DIR}/${base_name}"
  log "Building native shim (cross) for ${rid} (${CONFIG})"
  dotnet build "${ROOT_DIR}/OsCallsWindowsShim/OsCallsWindowsShim.csproj" -c "${CONFIG}" -r "${rid}" >/dev/null

  log "Publishing managed app for ${rid} (${CONFIG})"
  dotnet publish "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -c "${CONFIG}" -r "${rid}" \
    -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${out_dir}" >/dev/null

  # Copy native DLLs produced by the Windows shim build (copy all to be safe)
  local shim_bin="${ROOT_DIR}/OsCallsWindowsShim/build-win-x64/bin"
  if [[ -d "${shim_bin}" ]]; then
    shopt -s nullglob
    cp -f "${shim_bin}"/*.dll "${out_dir}/" || true
    shopt -u nullglob
  else
    log "WARN: missing ${shim_bin} (no DLLs copied)"
  fi

  # Archive (zip)
  (cd "${DIST_DIR}" && zip -qr "${base_name}.zip" ""$(basename "${out_dir}")"")
  log "Created ${DIST_DIR}/${base_name}.zip"
}

case "${TARGET}" in
  linux-x64) publish_linux ;;
  win-x64)   publish_windows ;;
  all)       publish_linux; publish_windows ;;
  *)         echo "Usage: $0 [linux-x64|win-x64|all] [Debug|Release]"; exit 2 ;;
esac

log "Done. See ${DIST_DIR}" 
