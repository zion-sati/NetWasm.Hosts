# Copyright (c) 2026 Zion Sati and NetWasm contributors (https://github.com/zion-sati/NetWasm.Hosts).
# SPDX-License-Identifier: MIT

"""Release versions and trains, as resolve-release.py reads them from a release tag."""

import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("resolve_release", Path(__file__).with_name("resolve-release.py"))
resolve = importlib.util.module_from_spec(spec)
spec.loader.exec_module(resolve)

COMMIT = "0" * 40
MANIFEST = {
    "schemaVersion": 2,
    "repository": "zion-sati/NetWasm.Hosts",
    "repositoryUrl": "https://github.com/zion-sati/NetWasm.Hosts",
    "releaseVersion": "0.15.2",
    "releaseTag": "v0.15.2",
    "sourceCommit": COMMIT,
    "dependencyVersions": {},
    "packages": ["NetWasm.Hosts"],
}


class ResolveReleaseTests(unittest.TestCase):
    def test_a_netwasm_version_and_a_revision_for_it_are_release_versions(self):
        for version in ("0.15.2", "0.15.2.1", "0.15.2.12", "0.15.2-preview.1", "0.15.2.1-preview.1"):
            with self.subTest(version=version):
                resolved = resolve.resolve_manifest(MANIFEST, "v" + version, COMMIT, "v")
                self.assertEqual(version, resolved["releaseVersion"])
                self.assertEqual("v" + version, resolved["releaseTag"])

    def test_a_zero_or_padded_revision_and_a_fifth_part_are_refused(self):
        for version in ("0.15.2.0", "0.15.2.01", "0.15.2.1.1", "0.15"):
            with self.subTest(version=version), self.assertRaisesRegex(ValueError, "supported semantic version"):
                resolve.resolve_manifest(MANIFEST, "v" + version, COMMIT, "v")

    def test_a_revisions_train_runs_from_its_preview_to_its_stable_version(self):
        build = resolve.resolve_train_identity("0.15.2.1-preview.1", "v")
        self.assertEqual("build", build["mode"])
        self.assertEqual("0.15.2.1-preview.1", build["previewVersion"])
        self.assertEqual("v0.15.2.1-preview.1", build["previewTag"])
        self.assertEqual("0.15.2.1", build["stableVersion"])
        self.assertEqual("v0.15.2.1", build["stableTag"])
        promote = resolve.resolve_train_identity("0.15.2.1", "v")
        self.assertEqual("promote", promote["mode"])
        self.assertEqual("0.15.2.1", promote["stableVersion"])

    def test_a_train_starts_at_preview_1(self):
        with self.assertRaisesRegex(ValueError, "preview.1"):
            resolve.resolve_train_identity("0.15.2.1-preview.2", "v")


if __name__ == "__main__":
    unittest.main()
