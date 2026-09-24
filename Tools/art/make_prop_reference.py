"""Reference pictures for props (Tools/art/props_spec.json) from the local ComfyUI Z-Image-Turbo, then a cut-out.

  bash Tools/art/comfy_server.sh &                                   # ComfyUI on 127.0.0.1:8188
  bash Tools/art/hy_python.sh Tools/art/make_prop_reference.py gen  <out_dir> [item ...] [--variants 4]
  # look at <out_dir>/<item>/sheet.png, stop ComfyUI (the GPU is needed by Hunyuan), then
  bash Tools/art/hy_python.sh Tools/art/make_prop_reference.py pick <out_dir> <item> <variant>

gen  writes <item>/ref_<n>.png (1024 px, seeds 20260925+n) and a 2x2 sheet.png for picking by eye.
pick removes the background (rembg u2net from the Hunyuan install, offline) and writes <item>/reference.png
(RGBA), the input of hunyuan_shape.py.
"""
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

SPEC = json.loads((Path(__file__).resolve().parent / "props_spec.json").read_text())
COMFY = "http://127.0.0.1:8188"
STYLE = ("{obj}. Single isolated object, stylized low-poly 3D game asset, chunky simple shapes, flat solid colours, "
         "no text, plain pure white background, soft even lighting, whole object visible and centred")
NEGATIVE = "photo, realistic texture, multiple objects, scene, ground, shadow, cropped, text, watermark"
SEED0 = 20260925
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))   # local server: never via the proxy


def workflow(prompt, seed):
    return {
        "1": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "z-image-turbo-Q8_0.gguf"}},
        "2": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["1", 0], "shift": 3.0}},
        "3": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen_3_4b_fp8_mixed.safetensors", "type": "lumina2", "device": "default"}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["3", 0], "text": prompt}},
        "5": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["3", 0], "text": NEGATIVE}},
        "6": {"class_type": "EmptySD3LatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
        "7": {"class_type": "KSampler", "inputs": {"model": ["2", 0], "positive": ["4", 0], "negative": ["5", 0],
                                                   "latent_image": ["6", 0], "seed": seed, "steps": 8, "cfg": 1.0,
                                                   "sampler_name": "res_multistep", "scheduler": "simple", "denoise": 1.0}},
        "8": {"class_type": "VAELoader", "inputs": {"vae_name": "ae.safetensors"}},
        "9": {"class_type": "VAEDecode", "inputs": {"samples": ["7", 0], "vae": ["8", 0]}},
        "10": {"class_type": "SaveImage", "inputs": {"images": ["9", 0], "filename_prefix": "carsgame_prop"}},
    }


def call(path, data=None):
    req = urllib.request.Request(COMFY + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={"Content-Type": "application/json"})
    with OPENER.open(req, timeout=600) as r:
        return r.read()


def render(prompt, seed, dst):
    pid = json.loads(call("/prompt", {"prompt": workflow(prompt, seed)}))["prompt_id"]
    for _ in range(600):
        hist = json.loads(call("/history/" + pid))
        if pid in hist:
            entry = hist[pid]
            if entry.get("status", {}).get("status_str") == "error":
                raise SystemExit(f"ComfyUI error: {json.dumps(entry['status'])[:600]}")
            img = entry["outputs"]["10"]["images"][0]
            q = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img["subfolder"], "type": img["type"]})
            dst.write_bytes(call("/view?" + q))
            return
        time.sleep(1)
    raise SystemExit("ComfyUI timeout")


def gen(out, items, variants):
    from PIL import Image
    for name in items:
        spec = SPEC["items"][name]
        d = out / name
        d.mkdir(parents=True, exist_ok=True)
        prompt = STYLE.format(obj=spec["prompt"][0].upper() + spec["prompt"][1:])
        (d / "prompt.txt").write_text(prompt + "\n")
        paths = []
        for n in range(variants):
            p = d / f"ref_{n}.png"
            t = time.monotonic()
            render(prompt, SEED0 + n, p)
            print(f"{name} ref_{n} {time.monotonic() - t:.1f}s", flush=True)
            paths.append(p)
        cols = 2
        rows = (len(paths) + 1) // 2
        sheet = Image.new("RGB", (512 * cols, 512 * rows), "white")
        for i, p in enumerate(paths):
            sheet.paste(Image.open(p).convert("RGB").resize((512, 512)), ((i % cols) * 512, (i // cols) * 512))
        sheet.save(d / "sheet.png")


def pick(out, name, variant):
    from PIL import Image
    from hy3dgen.rembg import BackgroundRemover
    d = out / name
    img = Image.open(d / f"ref_{variant}.png").convert("RGB")
    cut = BackgroundRemover()(img)
    cut.save(d / "reference.png")
    (d / "picked.txt").write_text(f"ref_{variant}.png seed {SEED0 + variant}\n")
    print("PICKED", d / "reference.png")


if __name__ == "__main__":
    a = sys.argv[1:]
    variants = 4
    if "--variants" in a:
        i = a.index("--variants")
        variants = int(a[i + 1])
        del a[i:i + 2]
    if len(a) < 2 or a[0] not in ("gen", "pick"):
        raise SystemExit(__doc__)
    out = Path(a[1]).expanduser().resolve()
    if a[0] == "gen":
        names = a[2:] or list(SPEC["items"])
        unknown = [n for n in names if n not in SPEC["items"]]
        if unknown:
            raise SystemExit(f"unknown items: {unknown}")
        gen(out, names, variants)
    else:
        pick(out, a[2], int(a[3]))
