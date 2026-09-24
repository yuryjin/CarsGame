"""Image -> raw shape with the local Hunyuan3D-2 (hunyuan3d-dit-v2-0, fp16), staged for a busy 12 GB GPU.

The stock --low_vram_mode moves the whole pipeline (conditioner 2.27 GB + DiT 2.23 GB + VAE 0.43 GB) to
the GPU at once. Here each component visits the GPU alone (conditioner -> DiT -> VAE) and goes back
to CPU afterwards, so the peak is ~2.6 GB. If a stage still hits CUDA OOM it is retried a few times
(another process may free memory); then the run exits with code 3. CPU fp32 is opt-in (--cpu_fallback).

Run with the Hunyuan venv (see README.md):
  HY=~/projects8/3d2/Hunyuan3D-2
  XDG_CACHE_HOME=$HY/.cache HY3DGEN_MODELS=$HY/.cache/hy3dgen $HY/.venv/bin/python hunyuan_shape.py \
      <reference.png> <out.glb> [--steps 30] [--octree 256] [--seed 20260924] [--guidance 5.0]
Writes <out.glb> plus <out>.json (timings, device per stage, peak VRAM, face count).
"""
import argparse
import json
import os
import sys
import time
from pathlib import Path

HY = Path(os.environ.get("HY3D_ROOT", Path.home() / "projects8/3d2/Hunyuan3D-2"))
sys.path.insert(0, str(HY))
os.environ.setdefault("XDG_CACHE_HOME", str(HY / ".cache"))
os.environ.setdefault("HY3DGEN_MODELS", str(HY / ".cache/hy3dgen"))
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

import numpy as np  # noqa: E402
import torch  # noqa: E402
torch.set_grad_enabled(False)   # inference only: without this autograd keeps every denoising step alive (8+ GB)
from PIL import Image  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument("image")
ap.add_argument("out")
ap.add_argument("--steps", type=int, default=30)
ap.add_argument("--octree", type=int, default=256)
ap.add_argument("--chunks", type=int, default=4000)
ap.add_argument("--seed", type=int, default=20260924)
ap.add_argument("--guidance", type=float, default=5.0)
ap.add_argument("--retries", type=int, default=4)
ap.add_argument("--retry_wait", type=float, default=60.0)
# Off by default: on 2026-09-24 the DiT in CPU fp32 grew to 51 GB RSS and took the desktop down with it.
ap.add_argument("--cpu_fallback", action="store_true", help="run a stage on CPU fp32 after the GPU retries (50+ GB RAM)")
args = ap.parse_args()

from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline, FloaterRemover, DegenerateFaceRemover  # noqa: E402
from hy3dgen.shapegen.pipelines import retrieve_timesteps, export_to_trimesh  # noqa: E402

report = {"model": "tencent/Hunyuan3D-2/hunyuan3d-dit-v2-0 (fp16)", "image": str(args.image), "steps": args.steps,
          "octree_resolution": args.octree, "seed": args.seed, "guidance": args.guidance, "stages": {}}
t_all = time.monotonic()

pipe = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained("tencent/Hunyuan3D-2", subfolder="hunyuan3d-dit-v2-0",
                                                        device="cpu", dtype=torch.float16)
report["load_seconds"] = round(time.monotonic() - t_all, 1)


def free_mib():
    f, _ = torch.cuda.mem_get_info()
    return f // (1 << 20)


def _hop_in(m, args_):    # hooks must return None, otherwise they replace the block's inputs/outputs
    m.to("cuda", non_blocking=True)


def _hop_out(m, args_, out):
    m.to("cpu")


def stream(module, block_lists, device, dtype):
    """Keep `module` resident on `device` except the blocks in `block_lists`, which hop to the GPU only for
    their own forward pass (sequential offload without accelerate). Returns a function that undoes it."""
    handles, blocks = [], [b for lst in block_lists for b in lst]
    ids = {id(p) for b in blocks for p in b.parameters()}
    for p in module.parameters():
        if id(p) not in ids:
            p.data = p.data.to(device, dtype)
    for b in blocks:
        b.to("cpu", dtype)
        for p in b.parameters():
            p.data = p.data.pin_memory()
        handles.append(b.register_forward_pre_hook(_hop_in))
        handles.append(b.register_forward_hook(_hop_out))

    def undo():
        for h in handles:
            h.remove()
        module.to("cpu")
    return undo


def staged(name, module, fn, block_lists=()):
    """Run fn(device, dtype) with `module` on the GPU. Attempt 1 loads it whole; later attempts stream its
    blocks (peak = one block + activations); CUDA OOM -> wait and retry; finally CPU fp32."""
    for attempt in range(args.retries):
        mode = "whole" if attempt == 0 or not block_lists else "stream"
        t = time.monotonic()
        undo = None
        try:
            torch.cuda.empty_cache()
            torch.cuda.reset_peak_memory_stats()
            before = free_mib()
            print(f"[{name}] attempt {attempt + 1} mode={mode} free={before} MiB own={torch.cuda.memory_allocated() >> 20} MiB", flush=True)
            if mode == "whole":
                module.to("cuda", torch.float16)
            else:
                undo = stream(module, block_lists, "cuda", torch.float16)
            out = fn("cuda", torch.float16)
            torch.cuda.synchronize()
            report["stages"][name] = {"device": "cuda", "mode": mode, "attempt": attempt + 1, "free_mib_before": before,
                                      "peak_mib": torch.cuda.max_memory_allocated() // (1 << 20),
                                      "seconds": round(time.monotonic() - t, 1)}
            return out
        except torch.cuda.OutOfMemoryError as e:
            msg = str(e)[:160]
            print(f"[{name}] CUDA OOM on attempt {attempt + 1} ({mode}, free {free_mib()} MiB): {msg}", flush=True)
            report["stages"].setdefault(name + "_oom", []).append({"attempt": attempt + 1, "mode": mode, "free_mib": free_mib()})
        finally:
            if undo:
                undo()
            module.to("cpu")
            torch.cuda.empty_cache()
        if attempt + 1 < args.retries:
            time.sleep(args.retry_wait if mode == "stream" else 2)
    if not args.cpu_fallback:
        print(f"[{name}] GPU busy after {args.retries} attempts; free VRAM (Unity, browser) and rerun, "
              f"or pass --cpu_fallback (needs 50+ GB RAM)", flush=True)
        sys.exit(3)
    print(f"[{name}] falling back to CPU fp32", flush=True)
    t = time.monotonic()
    module.to("cpu", torch.float32)
    out = fn("cpu", torch.float32)
    module.to(torch.float16)
    report["stages"][name] = {"device": "cpu-fp32", "seconds": round(time.monotonic() - t, 1)}
    return out


def to(x, device, dtype):
    if isinstance(x, torch.Tensor):
        return x.to(device, dtype if x.is_floating_point() else x.dtype)
    if isinstance(x, dict):
        return {k: to(v, device, dtype) for k, v in x.items()}
    return x


image = Image.open(args.image).convert("RGBA")
cond_inputs = pipe.prepare_image(image)
img_tensor = cond_inputs.pop("image")


def encode(device, dtype):
    cond = pipe.conditioner(image=img_tensor.to(device, dtype), **to(cond_inputs, device, dtype))
    un = pipe.conditioner.unconditional_embedding(img_tensor.shape[0], **to(cond_inputs, device, dtype))
    return {k: torch.cat([cond[k], un[k]], 0).to("cpu", torch.float16) for k in cond}


cond = staged("conditioner", pipe.conditioner, encode, [pipe.conditioner.main_image_encoder.model.encoder.layer])


def denoise(device, dtype):
    c = to(cond, device, dtype)
    # like the stock pipeline: sigmas start from 0
    timesteps, _ = retrieve_timesteps(pipe.scheduler, args.steps, device, sigmas=np.linspace(0, 1, args.steps))
    gen = torch.Generator().manual_seed(args.seed)
    latents = torch.randn((1, *pipe.vae.latent_shape), generator=gen).to(device, dtype)
    for i, t in enumerate(timesteps):
        x = torch.cat([latents] * 2)
        ts = (t.expand(x.shape[0]).to(latents.dtype) / pipe.scheduler.config.num_train_timesteps)
        noise = pipe.model(x, ts, c, guidance=None)
        n_c, n_u = noise.chunk(2)
        noise = n_u + args.guidance * (n_c - n_u)
        latents = pipe.scheduler.step(noise, t, latents).prev_sample
        if i % 5 == 0:
            print(f"  step {i + 1}/{len(timesteps)}", flush=True)
    return latents.to("cpu", torch.float16)


latents = staged("dit", pipe.model, denoise, [pipe.model.double_blocks, pipe.model.single_blocks])


def decode(device, dtype):
    lat = latents.to(device, dtype) / pipe.vae.scale_factor
    lat = pipe.vae(lat)
    return pipe.vae.latents2mesh(lat, bounds=1.01, mc_level=0.0, num_chunks=args.chunks,
                                 octree_resolution=args.octree, mc_algo=None, enable_pbar=False)


outputs = staged("vae", pipe.vae, decode, [pipe.vae.transformer.resblocks])
mesh = export_to_trimesh(outputs)[0]
report["faces_raw"] = int(len(mesh.faces))
mesh = FloaterRemover()(mesh)
mesh = DegenerateFaceRemover()(mesh)
report["faces_clean"] = int(len(mesh.faces))
report["bounds"] = mesh.bounds.tolist()
Path(args.out).parent.mkdir(parents=True, exist_ok=True)
mesh.export(args.out)
report["total_seconds"] = round(time.monotonic() - t_all, 1)
Path(args.out).with_suffix(".json").write_text(json.dumps(report, indent=2))
print("SHAPE OK", json.dumps(report), flush=True)
