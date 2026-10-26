"""A reused Unity QA copy must match source packages/settings as well as Assets."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

COMMON = Path(__file__).with_name('common.sh').resolve()

class SimSyncTests(unittest.TestCase):
    def test_reuse_updates_and_removes_inputs_without_touching_library(self):
        with tempfile.TemporaryDirectory(prefix='cars-sim-sync-') as folder:
            root = Path(folder)
            project, sim, work = root / 'project', root / 'sim', root / 'work'
            (sim / 'Library').mkdir(parents=True)
            (sim / 'Library' / 'sentinel').write_text('keep imported cache')
            for directory in ['Assets', 'Packages', 'ProjectSettings']:
                (project / directory).mkdir(parents=True)
                (sim / directory).mkdir(parents=True)
                (project / directory / 'current.txt').write_text('new ' + directory)
                (sim / directory / 'current.txt').write_text('old ' + directory)
                (sim / directory / 'deleted.txt').write_text('stale input')
            # The old script only refreshed this one settings file.
            (project / 'ProjectSettings' / 'ProjectSettings.asset').write_text('project')
            env = dict(os.environ, PROJECT=str(project), SIM=str(sim), CG_WORK=str(work))
            subprocess.run(['bash', '-c', 'set -e; source "$1"; ensure_sim', 'sync-test', str(COMMON)],
                           env=env, check=True, capture_output=True, text=True)
            for directory in ['Assets', 'Packages', 'ProjectSettings']:
                expected = {p.name: p.read_bytes() for p in (project / directory).iterdir()}
                actual = {p.name: p.read_bytes() for p in (sim / directory).iterdir()}
                self.assertEqual(expected, actual, directory)
            self.assertEqual('keep imported cache', (sim / 'Library' / 'sentinel').read_text())

if __name__ == '__main__':
    unittest.main()
