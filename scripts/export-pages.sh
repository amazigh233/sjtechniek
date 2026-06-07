#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${1:-"$ROOT_DIR/_site"}"
PUBLISH_DIR="$(mktemp -d)"
PORT="${PAGES_EXPORT_PORT:-5088}"
APP_PID=""

cleanup() {
    if [[ -n "$APP_PID" ]]; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi
    rm -rf "$PUBLISH_DIR"
}
trap cleanup EXIT

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet publish "$ROOT_DIR/SjTechniek.csproj" \
    --configuration Release \
    --output "$PUBLISH_DIR" \
    --nologo

(
    cd "$PUBLISH_DIR"
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS="http://127.0.0.1:$PORT" \
    dotnet SjTechniek.dll
) >"$PUBLISH_DIR/export-server.log" 2>&1 &
APP_PID=$!

curl --fail --silent --show-error \
    --retry 30 \
    --retry-delay 1 \
    --retry-connrefused \
    "http://127.0.0.1:$PORT/" \
    --output "$OUTPUT_DIR/index.html"

cp -R "$PUBLISH_DIR/wwwroot/." "$OUTPUT_DIR/"
touch "$OUTPUT_DIR/.nojekyll"

# GitHub Pages has no Blazor server. Keep the rendered HTML and remove the
# connection metadata and script that would otherwise try to contact it.
perl -0pi -e 's#<script type="importmap">.*?</script>##s' "$OUTPUT_DIR/index.html"
perl -0pi -e 's#<script src="_framework/blazor\.web\.[^"]+\.js"></script>##s' "$OUTPUT_DIR/index.html"
perl -0pi -e 's#<!--Blazor:.*?-->##gs' "$OUTPUT_DIR/index.html"
perl -0pi -e 's#<base href="/">#<base href="./">#g' "$OUTPUT_DIR/index.html"
perl -0pi -e 's#href="/"#href="./"#g; s#src="/cvketel-3d\.html"#src="./cvketel-3d.html"#g' "$OUTPUT_DIR/index.html"
perl -0pi -e 's#app\.[a-z0-9]+\.css#app.css#g; s#SjTechniek\.[a-z0-9]+\.styles\.css#SjTechniek.styles.css#g' "$OUTPUT_DIR/index.html"

rm -rf "$OUTPUT_DIR/_framework" "$OUTPUT_DIR/lib"

echo "Static site exported to $OUTPUT_DIR"
