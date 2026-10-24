#!/bin/bash
# strings.csv → CJK subset TTF (every CJK/symbol char used) → LocImporter + FontAtlasBaker in batch → copy tables/atlas back.
# Needs fontTools (pyftsubset) and the system Noto Sans CJK (face 2 = SC).
set -e
source "$(dirname "$0")/common.sh"
cd "$PROJECT"
python3 - "$WORK/font-unicodes.txt" <<'P'
import re, sys
from fontTools.ttLib import TTFont
cm = TTFont('Assets/CarsGame/Fonts/NotoSansSC-Subset.ttf').getBestCmap()
csv = open('Assets/CarsGame/Localization/Source/strings.csv', encoding='utf-8').read()
source = open('Assets/CarsGame/Editor/Localization/FontAtlasBaker.cs', encoding='utf-8').read()
symbols = re.search(r'public const string UiSymbols = "([^"]+)"', source).group(1)
extra = {ord(c) for c in csv if ord(c) > 0x2E80 or 0x2190 <= ord(c) <= 0x2BFF} | set(map(ord, symbols))
cps = sorted(set(cm) | extra)
open(sys.argv[1], 'w').write(','.join('U+%04X' % c for c in cps)); print('subset', len(cm), '->', len(cps))
P
pyftsubset /usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc --font-number=2 --unicodes-file="$WORK/font-unicodes.txt" --layout-features='' --output-file=Assets/CarsGame/Fonts/NotoSansSC-Subset.ttf
ensure_sim
cat > "$SIM/Assets/CarsGame/Editor/ZzBatchLoc.cs" <<'C'
namespace RacingGame.EditorTools { public static class ZzBatchLoc { public static void Run() { UnityEngine.Debug.Log("LOCRESULT " + LocImporter.Import()); UnityEngine.Debug.Log("BAKERESULT " + FontAtlasBaker.BakeChinese()); } } }
C
"$UNITY" -batchmode -nographics -projectPath "$SIM" -executeMethod RacingGame.EditorTools.ZzBatchLoc.Run -quit -logFile "$WORK/loc.log" >/dev/null 2>&1 || { grep -m10 "error CS\|Exception" "$WORK/loc.log" || true; exit 1; }
rm -f "$SIM/Assets/CarsGame/Editor/ZzBatchLoc.cs"*
grep -h "LOCRESULT\|BAKERESULT\|glyphs missing\|error CS" "$WORK/loc.log" | head
python3 - "$WORK/loc.log" <<'P'
import sys
log = open(sys.argv[1], encoding='utf-8').read()
results = [line for line in log.splitlines() if line.startswith('BAKERESULT ')]
assert results and "missing=''" in results[-1], 'Font bake must contain every requested character'
P
# Assets only (the main project's .meta files keep their GUIDs).
(cd "$SIM/Assets" && find Localization CarsGame/Fonts -type f ! -name '*.meta' -print0 | while IFS= read -r -d '' f; do
  if ! cmp -s "$f" "$PROJECT/Assets/$f"; then cp "$f" "$PROJECT/Assets/$f"; echo "updated $f"; fi; done)
