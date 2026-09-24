#!/bin/bash
# strings.csv → CJK subset TTF (every CJK/symbol char used) → LocImporter + FontAtlasBaker in batch → copy tables/atlas back.
# Needs fontTools (pyftsubset) and the system Noto Sans CJK (face 2 = SC).
set -e
source "$(dirname "$0")/common.sh"
cd "$PROJECT"
python3 - <<'P'
from fontTools.ttLib import TTFont
cm = TTFont('Assets/CarsGame/Fonts/NotoSansSC-Subset.ttf').getBestCmap()
csv = open('Assets/CarsGame/Localization/Source/strings.csv', encoding='utf-8').read()
extra = {ord(c) for c in csv if ord(c) > 0x2E80 or 0x2190 <= ord(c) <= 0x2BFF} | {0x2605, 0x2606, 0x00D7, 0x25B2}   # + FontAtlasBaker.UiSymbols
cps = sorted(set(cm) | extra)
open('/tmp/cars-qa-unicodes.txt', 'w').write(','.join('U+%04X' % c for c in cps)); print('subset', len(cm), '->', len(cps))
P
pyftsubset /usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc --font-number=2 --unicodes-file=/tmp/cars-qa-unicodes.txt --layout-features='' --output-file=Assets/CarsGame/Fonts/NotoSansSC-Subset.ttf
ensure_sim
cat > "$SIM/Assets/CarsGame/Editor/ZzBatchLoc.cs" <<'C'
namespace RacingGame.EditorTools { public static class ZzBatchLoc { public static void Run() { UnityEngine.Debug.Log("LOCRESULT " + LocImporter.Import()); UnityEngine.Debug.Log("BAKERESULT " + FontAtlasBaker.BakeChinese()); } } }
C
"$UNITY" -batchmode -nographics -projectPath "$SIM" -executeMethod RacingGame.EditorTools.ZzBatchLoc.Run -quit -logFile "$WORK/loc.log" >/dev/null 2>&1 || { grep -m10 "error CS\|Exception" "$WORK/loc.log" || true; exit 1; }
rm -f "$SIM/Assets/CarsGame/Editor/ZzBatchLoc.cs"*
grep -h "LOCRESULT\|BAKERESULT\|glyphs missing\|error CS" "$WORK/loc.log" | head
# Assets only (the main project's .meta files keep their GUIDs).
(cd "$SIM/Assets" && find Localization CarsGame/Fonts -type f ! -name '*.meta' -print0 | while IFS= read -r -d '' f; do
  if ! cmp -s "$f" "$PROJECT/Assets/$f"; then cp "$f" "$PROJECT/Assets/$f"; echo "updated $f"; fi; done)
