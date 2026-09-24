# Car art pipeline (Hunyuan3D-2 → Blender → Unity)

Contract: `spec.json` (sizes, wheel centres, turret, tri budget, ≤6 renderers). Everything is regenerable;
outputs live outside the repo in `~/projects8/3d2/outputs/<run>/`, only the final FBX goes into
`Assets/CarsGame/Art/Cars/HunyuanSample/Models/`.

```bash
O=~/projects8/3d2/outputs/carsgame-hunyuan-sample-2026-09-24
# 0. once after an OS upgrade: the venv needs CPython 3.10
uv python install 3.10
# 1. reference image (Blender, from the stock car proportions)
blender --background --factory-startup --python-exit-code 1 --python Tools/art/make_reference.py -- $O
# 2. shape: ~50 s on the RTX 4070 Ti, peak ~2.5 GB VRAM; RAM capped at 24 GB by hy_python.sh
bash Tools/art/hy_python.sh Tools/art/hunyuan_shape.py $O/reference.png $O/shape_raw.glb --steps 30 --octree 256
# 3. conform: orient, fit to spec bounds, cut/replace wheels, mirror, decimate to ~4.2k, vertex colours,
#    turret + barrel + Muzzle, FBX + validation.json (exit 1 when a check fails)
blender --background --factory-startup --python-exit-code 1 --python Tools/art/conform_car.py -- \
    $O/shape_raw.glb $O/reference.blend $O/conform
cp $O/conform/HunyuanSampleCar.fbx Assets/CarsGame/Art/Cars/HunyuanSample/Models/
# 4. prefab: Unity menu Racing/Art/Bake Hunyuan Sample Car (or RacingGame.EditorTools.Art.HunyuanSampleCarBaker.Bake()
#    via -executeMethod on a project copy, see Tools/qa/common.sh)
```

Never pass `--cpu_fallback` casually: the DiT in CPU fp32 grew to 51 GB RSS once and took the desktop down.
The GPU path needs `torch.set_grad_enabled(False)` (set in `hunyuan_shape.py`); without it autograd keeps
every denoising step and the DiT stage runs out of 12 GB.

# Props: pickups and weapon heads (Z-Image reference → Hunyuan3D-2 → Blender → Unity)

Contract: `props_spec.json` (bounds in the slot of the primitive the model replaces, triangle target, palette zones).
Outputs live in `~/projects8/3d2/outputs/props-<date>/<item>/`; only the final FBX goes into
`Assets/CarsGame/Art/Props/Models/<item>.fbx` (file name = library id).

```bash
O=~/projects8/3d2/outputs/props-2026-09-25
# 1. reference pictures: ComfyUI + Z-Image-Turbo (4 variants per item, pick one by eye from sheet.png)
bash Tools/art/comfy_server.sh &      # 127.0.0.1:8188; its venv python is broken, the script uses uv's 3.10
HY_MEM_MAX=4G bash Tools/art/hy_python.sh Tools/art/make_prop_reference.py gen $O [item ...]
kill %1                               # free the GPU: ComfyUI (~10 GB) and Hunyuan do not fit together
bash Tools/art/hy_python.sh Tools/art/make_prop_reference.py pick $O Pickup_Health 1   # cut-out -> reference.png
# 2. shape
bash Tools/art/hy_python.sh Tools/art/hunyuan_shape.py $O/Pickup_Health/reference.png $O/Pickup_Health/shape_raw.glb
# 3. conform: pose ('rot'), fit to bounds, mirror, decimate, zone colours, FBX + validation.json + contact sheet
blender --background --factory-startup --python-exit-code 1 --python Tools/art/conform_prop.py -- \
    $O/Pickup_Health/shape_raw.glb Pickup_Health $O/Pickup_Health/conform
cp $O/Pickup_Health/conform/Pickup_Health.fbx Assets/CarsGame/Art/Props/Models/
# 4. Unity: Racing/Art/Bake Prop Models (RacingGame.EditorTools.Art.PropModelLibraryBaker.Bake) ->
#    Resources/Art/PropModels.asset; the game falls back to primitives for anything missing
```

## Size and performance budget (all future art)

The game must stay small and fast (Yandex Games, browsers with 2 GB RAM and integrated graphics).
Build 0.2.0: zip 16.5 MB. Target ≤ 25 MB; art additions stop at **zip ≤ 18.5 MB** unless the owner decides otherwise.

| Asset | Triangles | Renderers | Notes |
|---|---|---|---|
| Pickup | 300–800 | 1 (+ the Beacon) | no shadows, glow via vertex alpha |
| Weapon head | 400–1200 | 1 | no shadows |
| Car body (`spec.json`) | body 3000–6000, total ≤ 7500 | ≤ 6 | |
| Props batch 2026-09-25 (14 models) | ≤ 12 000 total (actual 10 796) | | ≤ +250 KB zip, stop at +500 KB (actual +138 KB) |

Rules: flat faces and vertex colours only — no UVs, textures, tangents, animation or LODs; one shared material
per family on `CarsGame/StylizedLit` / `StylizedCarPaint` (SRP Batcher, no instancing); mesh compression Medium
for props; moving parts stay separate nodes (or procedural) instead of skinned meshes. Static decoration goes
through `PropBaker` + `MeshChunkWriter` (merged chunks, zero extra draw calls). Third-party models: CC0 only, one
model at a time through `conform_prop.py`, listed in `Docs/Release/ThirdPartyAssets.md`; source packs stay
outside the repository. Check every batch with `Tools/qa/build-web.sh` (build-size-report.txt) before merging.
