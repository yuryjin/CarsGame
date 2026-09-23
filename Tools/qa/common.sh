# Shared settings for the batch-Unity QA scripts. Source it; override with env vars.
PROJECT=${PROJECT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
UNITY=${UNITY:-$HOME/Unity/Hub/Editor/6000.5.9f1/Editor/Unity}
WORK=${CG_WORK:-/tmp/cars-qa}          # tmpfs is ideal: each project copy is ~3 GB
SIM=${SIM:-$WORK/sim}                  # a full project copy; one batch Unity per copy at a time
mkdir -p "$WORK"
# fontTools for loc-bake.sh lives in an older user site-packages on this machine.
export PYTHONPATH=${PYTHONPATH:-$HOME/.local/lib/python3.10/site-packages}

# First use: copy the project (with Library, so nothing reimports); later calls only resync Assets.
# Never run batch Unity on $PROJECT itself while the Editor has it open: it overwrites Logs/Editor.log
# and deletes the Pipeline bridge descriptor (Library/Pipeline/.unity-pipeline-port).
ensure_sim() {
  if [ ! -d "$SIM/Library" ]; then
    rsync -a --exclude Temp --exclude Logs --exclude Builds --exclude 'Library/Search' --exclude 'Library/Pipeline' \
      "$PROJECT/Assets" "$PROJECT/Packages" "$PROJECT/ProjectSettings" "$PROJECT/Library" "$SIM/"
    # A Library copied from an open Editor can map scene scripts to nothing ("referenced script is missing"):
    # drop the asset database once and let the copy reimport (~5 min).
    rm -rf "$SIM/Library/ArtifactDB"* "$SIM/Library/SourceAssetDB"* "$SIM/Library/Artifacts" "$SIM/Library/ScriptAssemblies" "$SIM/Library/Bee"
  fi
  rsync -a --delete "$PROJECT/Assets/" "$SIM/Assets/"
  cp "$PROJECT/ProjectSettings/ProjectSettings.asset" "$SIM/ProjectSettings/"
}

# Copy Unity-generated .meta files back for new assets the main project has no meta for yet.
sync_meta() {
  (cd "$SIM/Assets" && find . -name '*.meta' -print0 | while IFS= read -r -d '' m; do
    a="${m%.meta}"
    if [ -e "$PROJECT/Assets/$a" ] && [ ! -e "$PROJECT/Assets/$m" ]; then cp "$m" "$PROJECT/Assets/$m"; echo "meta $m"; fi
  done)
}
