#!/usr/bin/env python3
"""Record the actual isolated Unity inputs, including newly added source files.

Run after ensure_sim and before another resync of the same copy. Addressables'
content-state cache is build output, not an input. Tests are recorded separately.
"""
import hashlib
import json
import sys
from pathlib import Path

source, output = map(Path, sys.argv[1:3])
output.mkdir(parents=True, exist_ok=True)
runtime, tests, generated = {}, {}, {}
for folder in ("Assets", "Packages", "ProjectSettings"):
    for path in sorted((source / folder).rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(source).as_posix()
        is_generated = path.name == "addressables_content_state.bin" or relative.startswith((
            "Assets/InitTestScene", "Assets/Resources/PerformanceTestRun", "Assets/AddressableAssetsData/link.xml"))
        destination = generated if is_generated else tests if relative.startswith("Assets/CarsGame/Tests/") else runtime
        destination[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
for name, data in (("source-sha256.json", runtime), ("test-source-sha256.json", tests), ("generated-sha256.json", generated)):
    (output / name).write_text(json.dumps(data, indent=2) + "\n")
print(f"{len(runtime)} source/assets/settings inputs; {len(tests)} test inputs; {len(generated)} generated artifacts")
