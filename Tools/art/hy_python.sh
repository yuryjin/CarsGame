#!/bin/bash
# Run Python with the local Hunyuan3D-2 packages. The install's .venv/bin/python is a symlink to
# /usr/bin/python3, which the OS upgrade turned into 3.14 while the venv packages are cp310, so we run
# a standalone CPython 3.10 (uv python install 3.10) against the venv's site-packages instead.
HY=${HY3D_ROOT:-$HOME/projects8/3d2/Hunyuan3D-2}
PY=$(uv python find 3.10 2>/dev/null) || { echo "need: uv python install 3.10" >&2; exit 2; }
export PYTHONPATH="$HY/.venv/lib/python3.10/site-packages:$HY${PYTHONPATH:+:$PYTHONPATH}"
export XDG_CACHE_HOME="$HY/.cache" HF_HOME="$HY/.cache/huggingface" HY3DGEN_MODELS="$HY/.cache/hy3dgen" \
       U2NET_HOME="$HY/.cache/u2net" HF_HUB_OFFLINE=1
unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY all_proxy ALL_PROXY
# Hard RAM cap in its own cgroup: past HY_MEM_MAX the kernel kills this run only, not the desktop session
# (2026-09-24: an uncapped run hit 51 GB and systemd-oomd killed gnome-shell). HY_MEM_MAX=0 disables it.
MEM=${HY_MEM_MAX:-24G}
if [ "$MEM" != 0 ] && command -v systemd-run >/dev/null; then
  exec systemd-run --user --scope --quiet -p MemoryMax="$MEM" -p MemorySwapMax=0 "$PY" "$@"
fi
exec "$PY" "$@"
