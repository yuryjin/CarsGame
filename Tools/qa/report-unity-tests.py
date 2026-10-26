#!/usr/bin/env python3
"""Print Unity NUnit XML results; fail closed on absent, empty or unsuccessful runs."""
import sys
import xml.etree.ElementTree as ET


def report(path):
    try:
        root = ET.parse(path).getroot()
        total, passed, failed, skipped, inconclusive = (
            int(root.get(key, "0"))
            for key in ("total", "passed", "failed", "skipped", "inconclusive")
        )
    except (OSError, ET.ParseError, ValueError) as error:
        print(f"Invalid Unity test report: {error}", file=sys.stderr)
        return 1

    print(f"total={total} passed={passed} failed={failed} skipped={skipped} inconclusive={inconclusive}")
    cases = list(root.iter("test-case"))
    for case in cases:
        if case.get("result") != "Passed":
            message = case.findtext("failure/message") or case.findtext("reason/message") or ""
            print(f"{case.get('result')}: {case.get('fullname')}\n{message[:2000]}")
    valid = (
        root.tag == "test-run" and root.get("result") == "Passed"
        and total > 0 and passed > 0 and failed == 0 and inconclusive == 0
        and skipped >= 0 and total == passed + skipped and len(cases) == total
        and sum(case.get("result") == "Passed" for case in cases) == passed
        and all(case.get("result") in ("Passed", "Skipped") for case in cases)
    )
    if not valid:
        print("Unity test run did not pass or did not execute a complete, non-empty selection.", file=sys.stderr)
    return 0 if valid else 1


if __name__ == "__main__":
    sys.exit(report(sys.argv[1]))
