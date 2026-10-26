# Grandstand visual refresh — 2026-10-25

The existing six-row, 44 m grandstand now has four seating sectors, shallow stair treads, yellow step edges, railings, individual seats, seated spectator silhouettes, a framed canopy with a light soffit, and checkerboard front panels. Placement and collision layout are unchanged.

`GrandstandGeometry.cs` generates static vertex-coloured geometry through `PropBaker`. Its vertex alpha enables a small (.16) indirect fill through the existing environment material, keeping details readable beneath the canopy. Other generated props have zero alpha. Both prop baking and scene consolidation preserve this material setting.

Validation: Unity 6000.5.9f1 compiled and completed the bake with exit code 0 in an isolated project copy on NVIDIA GeForce RTX 4070 Ti / Vulkan. Inspected close and wide day/night editor renders. The updated chunk has 64,368 vertices, below the writer's 65,000 limit. All props still use 31 renderers across 29 chunks. Hash comparison of all 29 regenerated prop chunks found only `Props_0_0_0.asset` changed. Copied that asset back while retaining its original metadata/GUID and scene references. No scene rewrite or collider changes were copied back. `git diff --check` passed for the changed tracked files.

The QA copy used embedded copies of the installed packages and a disabled external package registry to avoid registry timeouts. Main project package settings are unchanged. No WebGL rebuild or gameplay test suite was run for this static art change.

- [Before](before.png)
- [After, close](after.png)
- [After, wide](wide.png)
- [Night](night.png)

Rebuild through **Racing → Environment → Bake Props** when editing the generated model; the updated mesh is already installed in the project.
