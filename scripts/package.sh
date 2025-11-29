#!/usr/bin/env bash
set -euo pipefail

# Provide friendly error messages when the script exits prematurely.
die() {
	echo "[package] ERROR: $*" >&2
	exit 1
}
# Track the last command for helpful diagnostics on failure.
last_cmd=""
trap 'last_cmd="$BASH_COMMAND"' DEBUG
trap 'rc=$?; if [ $rc -ne 0 ]; then echo "[package] ERROR: Command \"$last_cmd\" failed with exit code $rc (line ${BASH_LINENO[0]})" >&2; fi' EXIT

# Ensure dotnet is available before doing any work (it's required for all actions)
if ! command -v dotnet >/dev/null 2>&1; then
	die "dotnet is required to build and package DeDuBa. Please install the .NET SDK (https://dotnet.microsoft.com/)."
fi

# Package DeDuBa into distributable archives for linux-x64 and/or win-x64.
# Produces self-contained single-file builds plus required native shims.
#
# Usage:
#   scripts/package.sh [linux-x64|win-x64|all|clean] [Debug|Release] [--keep=N]
#
# Commands:
#   linux-x64, win-x64, all - Build and package for specified platform(s)
#   clean                   - Remove old versioned artifacts (keeps last 3 by default)
#
# Options:
#   --keep=N               - When cleaning, keep last N versions (default: 3)
#
# Outputs (version from MinVer):
#   dist/DeDuBa-<version>-<rid>/ ... files ...
#   dist/DeDuBa-<version>-<rid>.tar.gz (Linux)
#   dist/DeDuBa-<version>-<rid>.zip    (Windows)

ROOT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
DIST_DIR="${ROOT_DIR}/dist"
CONFIG="${2:-Release}"
TARGET="${1:-all}"
KEEP_VERSIONS=3

# Parse --keep=N option from arguments
for arg in "$@"; do
	if [[ $arg == --keep=* ]]; then
		KEEP_VERSIONS="${arg#*=}"
		if ! [[ $KEEP_VERSIONS =~ ^[0-9]+$ ]] || [ "$KEEP_VERSIONS" -lt 1 ]; then
			die "--keep value must be a positive integer, got: $KEEP_VERSIONS"
		fi
	fi
done

mkdir -p "${DIST_DIR}"

log() { echo "[package] $*"; }

# Clean old versioned artifacts, keeping the last N versions
clean_old_artifacts() {
	local rid="$1"
	local keep="${2:-3}"

	log "Cleaning old artifacts for $rid (keeping last $keep versions)..."

	# Find all versioned directories for this RID, sorted by modification time (newest first)
	local dirs
	dirs=$(find "${DIST_DIR}" -maxdepth 1 -type d -name "DeDuBa-*-${rid}" | sort -r)

	if [ -z "$dirs" ]; then
		log "No versioned directories found for $rid"
		return 0
	fi

	local count=0
	while IFS= read -r dir; do
		count=$((count + 1))
		if [ $count -gt "$keep" ]; then
			local base_name
			base_name=$(basename "$dir")
			log "Removing old artifact: $base_name"
			rm -rf "$dir"
			# Also remove associated archives
			rm -f "${DIST_DIR}/${base_name}.tar.gz" "${DIST_DIR}/${base_name}.tar.gz.sha512"
			rm -f "${DIST_DIR}/${base_name}.zip" "${DIST_DIR}/${base_name}.zip.sha512"
		fi
	done <<<"$dirs"

	log "Cleanup complete for $rid"
}

check_cmd() {
	if ! command -v "$1" >/dev/null 2>&1; then
		die "Required command '$1' not found. Please install it and re-run this script."
	fi
}

get_version() {
	# Prefer msbuild property if available; fallback to parsing build output.
	if VERSION_LINE=$(dotnet msbuild "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -nologo -t:MinVer -getProperty:MinVerVersion 2>/dev/null); then
		[[ -n ${VERSION_LINE} ]] && echo "${VERSION_LINE}" && return 0
	fi
	dotnet build "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -c "${CONFIG}" -nologo |
		awk '/MinVer: Calculated version/{print $4; exit}' || echo "0.0.0-unknown"
}

VERSION="$(get_version)"
log "Resolved version: ${VERSION}"

publish_linux() {
	local rid="linux-x64"
	local base_name="DeDuBa-${VERSION}-${rid}"
	local out_dir="${DIST_DIR}/${base_name}"
	log "Building native shims for ${rid} (${CONFIG})"
	dotnet build "${ROOT_DIR}/OsCallsCommonShim/OsCallsCommonShim.csproj" -c "${CONFIG}" >/dev/null || die "Building OsCallsCommonShim failed"
	dotnet build "${ROOT_DIR}/OsCallsLinuxShim/OsCallsLinuxShim.csproj" -c "${CONFIG}" >/dev/null || die "Building OsCallsLinuxShim failed"

	log "Publishing managed app for ${rid} (${CONFIG})"
	dotnet publish "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -c "${CONFIG}" -r "${rid}" \
		-p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true \
		-o "${out_dir}" >/dev/null || die "dotnet publish failed for ${rid}"

	# Copy native shims next to the binary
	local shim_common="${ROOT_DIR}/OsCallsCommonShim/bin/${CONFIG}/net8.0/libOsCallsCommonShim.so"
	local shim_linux="${ROOT_DIR}/OsCallsLinuxShim/bin/${CONFIG}/net8.0/libOsCallsLinuxShim.so"
	if [[ -f ${shim_common} ]]; then cp -f "${shim_common}" "${out_dir}/"; else log "WARN: missing ${shim_common}"; fi
	if [[ -f ${shim_linux} ]]; then cp -f "${shim_linux}" "${out_dir}/"; else log "WARN: missing ${shim_linux}"; fi

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
	check_cmd tar
	(cd "${DIST_DIR}" && tar czf "${base_name}.tar.gz" "$(basename "${out_dir}")")
	log "Created ${DIST_DIR}/${base_name}.tar.gz"
	# Generate SHA-512 checksum for the archive
	if command -v sha512sum >/dev/null 2>&1; then
		sha512sum "${DIST_DIR}/${base_name}.tar.gz" >"${DIST_DIR}/${base_name}.tar.gz.sha512"
		log "Created ${DIST_DIR}/${base_name}.tar.gz.sha512"
	else
		log "WARN: sha512sum not available; skipping checksum generation for ${base_name}.tar.gz"
	fi
}

publish_windows() {
	local rid="win-x64"
	local base_name="DeDuBa-${VERSION}-${rid}"
	local out_dir="${DIST_DIR}/${base_name}"
	log "Building native shim (cross) for ${rid} (${CONFIG})"
	dotnet build "${ROOT_DIR}/OsCallsWindowsShim/OsCallsWindowsShim.csproj" -c "${CONFIG}" -r "${rid}" >/dev/null || die "Building OsCallsWindowsShim for ${rid} failed"

	log "Publishing managed app for ${rid} (${CONFIG})"
	dotnet publish "${ROOT_DIR}/DeDuBa/DeDuBa.csproj" -c "${CONFIG}" -r "${rid}" \
		-p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true \
		-o "${out_dir}" >/dev/null || die "dotnet publish failed for ${rid}"

	# Copy native DLLs produced by the Windows shim build (copy all to be safe)
	local shim_bin="${ROOT_DIR}/OsCallsWindowsShim/build-win-x64/bin"
	if [[ -d ${shim_bin} ]]; then
		shopt -s nullglob
		cp -f "${shim_bin}"/*.dll "${out_dir}/" || true
		shopt -u nullglob
	else
		log "WARN: missing ${shim_bin} (no DLLs copied)"
	fi

	# Archive (zip)
	check_cmd zip
	(cd "${DIST_DIR}" && zip -qr "${base_name}.zip" "$(basename "${out_dir}")")
	log "Created ${DIST_DIR}/${base_name}.zip"
	# Generate SHA-512 checksum for the zip
	if command -v sha512sum >/dev/null 2>&1; then
		sha512sum "${DIST_DIR}/${base_name}.zip" >"${DIST_DIR}/${base_name}.zip.sha512"
		log "Created ${DIST_DIR}/${base_name}.zip.sha512"
	else
		log "WARN: sha512sum not available; skipping checksum generation for ${base_name}.zip"
	fi
}

case "${TARGET}" in
linux-x64) publish_linux ;;
win-x64) publish_windows ;;
all)
	publish_linux
	publish_windows
	;;
clean)
	log "Starting cleanup of old artifacts (keeping last $KEEP_VERSIONS versions)..."
	clean_old_artifacts "linux-x64" "$KEEP_VERSIONS"
	clean_old_artifacts "win-x64" "$KEEP_VERSIONS"
	log "Cleanup complete"
	exit 0
	;;
*)
	echo "Usage: $0 [linux-x64|win-x64|all|clean] [Debug|Release] [--keep=N]"
	echo ""
	echo "Commands:"
	echo "  linux-x64, win-x64, all  - Build and package for specified platform(s)"
	echo "  clean                    - Remove old versioned artifacts (keeps last 3 by default)"
	echo ""
	echo "Options:"
	echo "  --keep=N                 - When cleaning, keep last N versions (default: 3)"
	exit 2
	;;
esac

log "Done. See ${DIST_DIR}"
