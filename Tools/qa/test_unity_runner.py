"""Fast runner regression checks, independent of a Unity installation."""
import contextlib
import importlib.util
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET


QA_DIR = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("unity_report", QA_DIR / "report-unity-tests.py")
unity_report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(unity_report)


def result_xml(results=("Passed",), outcome="Passed"):
    root = ET.Element("test-run", result=outcome, total=str(len(results)))
    for key in ("passed", "failed", "skipped", "inconclusive"):
        root.set(key, str(results.count(key.capitalize())))
    suite = ET.SubElement(root, "test-suite", result=outcome)
    for i, result in enumerate(results):
        case = ET.SubElement(suite, "test-case", fullname=f"Example.Test{i}", result=result)
        if result == "Failed":
            ET.SubElement(ET.SubElement(case, "failure"), "message").text = "Expected 6 stars, got 5"
    return ET.tostring(root, encoding="unicode")


class ReportTests(unittest.TestCase):
    def check_report(self, xml, expected):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "result.xml"
            if xml is not None:
                path.write_text(xml)
            output = io.StringIO()
            with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                self.assertEqual(expected, unity_report.report(path))
            return output.getvalue()

    def test_passes_valid_report_regardless_of_attribute_order(self):
        self.check_report(result_xml(), 0)

    def test_reports_failure_name_and_message(self):
        output = self.check_report(result_xml(("Failed",), "Failed"), 1)
        self.assertIn("Example.Test0", output)
        self.assertIn("Expected 6 stars, got 5", output)

    def test_rejects_missing_truncated_and_invalid_reports(self):
        for xml in (None, "", "<test-run", "<test-run total='bad' />"):
            with self.subTest(xml=xml):
                self.check_report(xml, 1)

    def test_rejects_filter_that_matches_no_tests(self):
        self.check_report(result_xml(()), 1)

    def test_rejects_all_skipped(self):
        self.check_report(result_xml(("Skipped",)), 1)

    def test_reports_skips_but_accepts_successfully_executed_selection(self):
        output = self.check_report(result_xml(("Passed", "Skipped")), 0)
        self.assertIn("skipped=1", output)

    def test_rejects_inconclusive_or_failed_parent_suite(self):
        self.check_report(result_xml(("Inconclusive",), "Inconclusive"), 1)
        self.check_report(result_xml(("Passed",), "Failed"), 1)

    def test_rejects_inconsistent_counts_or_missing_cases(self):
        self.check_report(result_xml().replace('passed="1"', 'passed="2"'), 1)
        self.check_report(result_xml().replace('<test-case fullname="Example.Test0" result="Passed" />', ''), 1)


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        project = self.folder / "source"
        sim = self.folder / "sim"
        for base in (project, sim):
            for name in ("Assets", "Packages", "ProjectSettings", "Library"):
                (base / name).mkdir(parents=True)
        (project / "ProjectSettings/ProjectSettings.asset").write_text("test settings")
        self.fake_unity = self.folder / "fake-unity"
        self.fake_unity.write_text("""#!/usr/bin/env python3
import json, os, pathlib, subprocess, sys, time
args = sys.argv[1:]
time.sleep(float(os.environ.get('FAKE_DELAY', '0')))
if os.environ.get('FAKE_BACKGROUND') == '1':
    subprocess.Popen(['/bin/sleep', '0.5'], close_fds=False, start_new_session=True,
                     stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
pathlib.Path(os.environ['ARGUMENTS']).write_text(json.dumps(args))
pathlib.Path(args[args.index('-logFile') + 1]).write_text('test runner fixture')
if os.environ.get('FAKE_NO_RESULT') != '1':
    pathlib.Path(args[args.index('-testResults') + 1]).write_text(os.environ['FAKE_XML'])
sys.exit(int(os.environ.get('FAKE_EXIT', '0')))
""")
        self.fake_unity.chmod(0o755)
        self.env = {**os.environ, "PROJECT": str(project), "SIM": str(sim),
                    "CG_WORK": str(self.folder / "work"), "UNITY": str(self.fake_unity),
                    "CG_QA_CONFIG_HOME": str(self.folder / "config"), "CG_TEST_TAG": "runner-test",
                    "CG_CPU_SET": str(min(os.sched_getaffinity(0))), "CG_TEST_TIMEOUT": "10",
                    "ARGUMENTS": str(self.folder / "arguments.json"), "FAKE_XML": result_xml()}

    def run_runner(self, script="edit-tests.sh", *args):
        return subprocess.run(["bash", str(QA_DIR / script), *args], env=self.env,
                              capture_output=True, text=True, timeout=20)

    def test_wrapper_forwards_filter_and_returns_success(self):
        import json
        result = self.run_runner("edit-tests.sh", "SaveMergeInvariantTests;RaceCupBoundaryTests")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        args = json.loads((self.folder / "arguments.json").read_text())
        self.assertEqual("EditMode", args[args.index("-testPlatform") + 1])
        self.assertEqual("SaveMergeInvariantTests;RaceCupBoundaryTests", args[args.index("-testFilter") + 1])

    def test_failed_xml_overrides_zero_unity_exit(self):
        self.env["FAKE_XML"] = result_xml(("Failed",), "Failed")
        self.assertEqual(1, self.run_runner().returncode)

    def test_unity_failure_overrides_successful_xml(self):
        self.env["FAKE_EXIT"] = "7"
        self.assertEqual(7, self.run_runner().returncode)

    def test_timeout_returns_124_and_does_not_report_success(self):
        self.env["FAKE_DELAY"] = "5"
        self.env["CG_TEST_TIMEOUT"] = "0.1"
        self.assertEqual(124, self.run_runner().returncode)

    def test_background_service_cannot_keep_project_locked_after_unity_exits(self):
        self.env["FAKE_BACKGROUND"] = "1"
        self.assertEqual(0, self.run_runner().returncode)
        result = subprocess.run(["flock", "-n", str(self.folder / "sim/.qa-tests.lock"), "true"], timeout=5)
        self.assertEqual(0, result.returncode)

    def test_stale_success_is_removed_before_a_run_that_produces_no_report(self):
        self.assertEqual(0, self.run_runner().returncode)
        self.env["FAKE_NO_RESULT"] = "1"
        self.assertEqual(1, self.run_runner().returncode)

    def test_all_runs_both_suites_with_separate_artifacts(self):
        self.env["CG_NOGRAPHICS"] = "1"
        result = self.run_runner("tests.sh", "all")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        for suite in ("edit", "play"):
            self.assertTrue((self.folder / f"work/runner-test-{suite}.xml").is_file())


if __name__ == "__main__":
    unittest.main()
