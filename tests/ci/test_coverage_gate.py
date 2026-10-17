# Copyright © 2026 myNOC LLC. All rights reserved.
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("gate", Path(__file__).parents[2] / "scripts/check_coverage.py")
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class CoverageGateTests(unittest.TestCase):
    def test_relative_report_paths_use_source_roots(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "coverage.xml"
            report.write_text('<coverage><sources><source>D:/repo/src/</source></sources><class filename="MorseCodeTrainer.Core/A.cs"><lines><line number="1" hits="1"/></lines></class></coverage>')
            self.assertEqual({"src/MorseCodeTrainer.Core/A.cs": {1: True}}, gate.read_coverage(str(report)))

    def test_javascript_patch_coverage_and_missing_report(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "coverage.lcov"
            report.write_text('SF:src/MorseCodeTrainer.UI/wwwroot/media.js\nDA:1,1\nDA:2,0\nend_of_record\n')
            coverage = {}
            gate.add_lcov(coverage, report)
            self.assertFalse(gate.check(coverage, {"src/MorseCodeTrainer.UI/wwwroot/media.js": {2}}, 90))
            report.write_text('')
            with self.assertRaises(ValueError):
                gate.add_lcov({}, report)

    def test_hunks_include_added_lines_but_not_deletions(self):
        diff = "+++ b/src/MorseCodeTrainer.Core/A.cs\n@@ -1,0 +2,3 @@\n+x\n@@ -7 +10 @@\n+x\n@@ -20,3 +22,0 @@"
        self.assertEqual({"src/MorseCodeTrainer.Core/A.cs": {2, 3, 4, 10}}, gate.parse_diff(diff))

    def test_patch_can_fail_when_total_passes(self):
        path = "src/MorseCodeTrainer.Core/A.cs"
        coverage = {path: {line: line < 100 for line in range(1, 101)}}
        self.assertFalse(gate.check(coverage, {path: {100}}, 90))
        self.assertTrue(gate.check(coverage, {path: {1}}, 90))

    def test_absent_reports_or_changed_files_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                gate.read_coverage(directory + "/missing.xml")
        with self.assertRaises(ValueError):
            gate.check({"src/MorseCodeTrainer.Core/A.cs": {1: True}}, {"src/MorseCodeTrainer.Core/B.cs": {1}}, 90)

    def test_no_executable_changes_still_requires_total_coverage(self):
        self.assertFalse(gate.check({"src/MorseCodeTrainer.Core/A.cs": {1: False}}, {}, 90))

    def test_merges_duplicate_classes_and_normalizes_windows_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "coverage.xml"
            report.write_text('<coverage><class filename="D:\\repo\\src\\MorseCodeTrainer.Core\\A.cs"><lines><line number="1" hits="0"/></lines></class><class filename="src/MorseCodeTrainer.Core/A.cs"><lines><line number="1" hits="1"/></lines></class></coverage>')
            self.assertEqual({"src/MorseCodeTrainer.Core/A.cs": {1: True}}, gate.read_coverage(str(report)))


if __name__ == "__main__":
    unittest.main()
