#!/usr/bin/env bash
# Standalone public-repo package. The desktop repo's scripts/package-plugin.sh
# produces the same install layout for signed Get Plugins releases.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dotnet_bin="$(command -v dotnet || true)"
if [[ -z "$dotnet_bin" && -x "$HOME/.dotnet/dotnet" ]]; then dotnet_bin="$HOME/.dotnet/dotnet"; fi
if [[ -z "$dotnet_bin" ]]; then echo ".NET 8 SDK is required" >&2; exit 1; fi
version="$(python3 - "$repo/plugin.json" <<'PY'
import json, sys
print(json.load(open(sys.argv[1]))['version'])
PY
)"
"$dotnet_bin" run --project "$repo/tests/EQ2Advanced.ReactiveTracker.Checks"
node "$repo/tests/ui-check.mjs"
stage="$repo/dist/reactive-tracker"
rm -rf "$stage"
"$dotnet_bin" publish "$repo/src/EQ2Advanced.ReactiveTracker/EQ2Advanced.ReactiveTracker.csproj" -c Release -o "$stage" >/dev/null
rm -f "$stage/EQ2Advanced.Plugins.dll" "$stage"/*.pdb "$stage"/*.runtimeconfig.json
cp "$repo/plugin.json" "$repo/README.md" "$repo/LICENSE" "$stage/"
cp -r "$repo/ui" "$stage/ui"
python3 - "$repo/dist" "$version" <<'PY'
import pathlib, sys, zipfile
root, version = pathlib.Path(sys.argv[1]), sys.argv[2]
dest = root / f'EQ2Advanced-Reactives-{version}.zip'
with zipfile.ZipFile(dest, 'w', zipfile.ZIP_DEFLATED) as archive:
    for file in sorted((root / 'reactive-tracker').rglob('*')):
        if file.is_file(): archive.write(file, file.relative_to(root))
print(dest)
PY
