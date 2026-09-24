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
