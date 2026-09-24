#!/bin/bash
# Start the local ComfyUI (text -> image for prop reference pictures) on 127.0.0.1:8188.
# Its venv/bin/python points at a deleted virtualenv, so, like hy_python.sh, we run uv's CPython 3.10
# against the venv's site-packages, in a RAM-capped scope. Stop it (Ctrl+C / kill) before running
# hunyuan_shape.py: both on the 12 GB GPU at once do not fit.
CUI=${COMFYUI_ROOT:-$HOME/comfyui}
PY=$(uv python find 3.10 2>/dev/null) || { echo "need: uv python install 3.10" >&2; exit 2; }
export PYTHONPATH="$CUI/venv/lib/python3.10/site-packages${PYTHONPATH:+:$PYTHONPATH}"
unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY all_proxy ALL_PROXY
cd "$CUI" || exit 2
MEM=${COMFY_MEM_MAX:-20G}
ARGS=(main.py --listen 127.0.0.1 --port "${COMFY_PORT:-8188}" --disable-auto-launch)
if [ "$MEM" != 0 ] && command -v systemd-run >/dev/null; then
  exec systemd-run --user --scope --quiet -p MemoryMax="$MEM" -p MemorySwapMax=0 "$PY" "${ARGS[@]}"
fi
exec "$PY" "${ARGS[@]}"
