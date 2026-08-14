from __future__ import annotations

import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest


TOOL_DIR = Path(__file__).resolve().parents[1]
PROJECT_ROOT = TOOL_DIR.parents[1]
sys.path.insert(0, str(TOOL_DIR))

from worldbuilder_core import DEFAULT_WORLD, WorldModel, export_preview, validate_world  # noqa: E402


class WorldBuilderCoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self.model = WorldModel.load(DEFAULT_WORLD)

    def error_codes(self, data=None) -> set[str]:
        return {issue.code for issue in validate_world(data or self.model.data) if issue.severity == "error"}

    def test_project_world_passes_editor_validation(self) -> None:
        self.assertEqual(set(), self.error_codes())

    def test_duplicate_and_malformed_site_ids_are_plain_errors(self) -> None:
        data = copy.deepcopy(self.model.data)
        data["Sites"][1]["Id"] = data["Sites"][0]["Id"]
        data["Sites"][2]["Id"] = "Bad ID"
        codes = self.error_codes(data)
        self.assertIn("site.id_duplicate", codes)
        self.assertIn("site.id_format", codes)

    def test_spawn_must_be_above_water_and_over_land(self) -> None:
        data = copy.deepcopy(self.model.data)
        data["Sites"][0]["TravelPosition"] = {"x": 0.0, "y": data["WaterLevel"], "z": 0.0}
        codes = self.error_codes(data)
        self.assertIn("site.underwater", codes)
        self.assertIn("site.off_land", codes)

    def test_python_preflight_matches_unity_city_biome_seed_and_padding_rules(self) -> None:
        data = copy.deepcopy(self.model.data)
        data["Landmasses"][0]["Biome"] = "UnknownBiome"
        data["Landmasses"][1]["TerrainSeed"] = data["Landmasses"][0]["TerrainSeed"]
        data["Sites"][0]["Id"] = "different_city"
        data["Landmasses"][0]["Center"]["z"] += 1.0
        codes = self.error_codes(data)
        self.assertIn("land.biome_unknown", codes)
        self.assertIn("land.seed_duplicate", codes)
        self.assertIn("city.site_missing", codes)
        self.assertIn("city.land_missing", codes)
        self.assertIn("land.outside_map", codes)

    def test_reversed_map_bounds_are_not_silently_repaired(self) -> None:
        data = copy.deepcopy(self.model.data)
        data["MapMinX"], data["MapMaxX"] = data["MapMaxX"], data["MapMinX"]
        self.assertIn("world.bounds_order", self.error_codes(data))

    def test_ocean_city_pairing_and_site_kind_cannot_disagree_with_unity(self) -> None:
        data = copy.deepcopy(self.model.data)
        data["OceanSize"] = 100.0
        data["Landmasses"][1]["CityName"] = "Orphan City"
        site_id = data["Sites"][4]["Id"]
        data["_WorldBuilder"]["SiteKinds"][site_id] = "city"
        codes = self.error_codes(data)
        self.assertIn("world.ocean_bounds", codes)
        self.assertIn("land.city_id_missing", codes)
        self.assertIn("site.kind_mismatch", codes)

    def test_roads_need_unique_ids_two_points_and_dry_ends(self) -> None:
        data = copy.deepcopy(self.model.data)
        data["_WorldBuilder"]["RoadIds"][1] = data["_WorldBuilder"]["RoadIds"][0]
        data["Roads"][0]["Points"] = [{"x": 9000.0, "y": 0.0, "z": 9000.0}]
        codes = self.error_codes(data)
        self.assertIn("road.id_duplicate", codes)
        self.assertIn("road.short", codes)

    def test_gate_and_story_spawn_are_editor_only_metadata(self) -> None:
        runtime_site_count = len(self.model.data["Sites"])
        marker_kind, marker_index = self.model.add_site("gate", -2000.0, 24.0, 1600.0)
        self.assertEqual(("marker", 0), (marker_kind, marker_index))
        self.assertEqual(runtime_site_count, len(self.model.data["Sites"]))
        self.assertEqual("gate", self.model.data["_WorldBuilder"]["Markers"][0]["Kind"])
        self.assertEqual(set(), self.error_codes())

    def test_undo_and_redo_restore_exact_document(self) -> None:
        before = self.model.snapshot()
        self.model.add_site("story_spawn", -2000.0, 24.0, 1600.0)
        after = self.model.snapshot()
        self.assertNotEqual(before, after)
        self.assertTrue(self.model.undo())
        self.assertEqual(before, self.model.data)
        self.assertTrue(self.model.redo())
        self.assertEqual(after, self.model.data)

    def test_atomic_save_creates_backup_and_keeps_runtime_fields(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder) / "test.world.json"
            target.write_text(DEFAULT_WORLD.read_text(encoding="utf-8"), encoding="utf-8")
            model = WorldModel.load(target)
            original_runtime_keys = set(json.loads(target.read_text(encoding="utf-8")))
            backup = model.save()
            self.assertIsNotNone(backup)
            self.assertTrue(backup.exists())
            self.assertNotEqual(target.parent, backup.parent)
            saved = json.loads(target.read_text(encoding="utf-8"))
            self.assertTrue(original_runtime_keys.issubset(saved.keys()))
            self.assertIn("_WorldBuilder", saved)
            backup.unlink()

    def test_headless_png_and_svg_exports(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            png = export_preview(self.model.data, Path(folder) / "preview.png", 320, 240)
            svg = export_preview(self.model.data, Path(folder) / "preview.svg", 320, 240)
            self.assertEqual(b"\x89PNG\r\n\x1a\n", png.read_bytes()[:8])
            self.assertIn("<svg", svg.read_text(encoding="utf-8"))
            self.assertIn("Ratna Bay world preview", svg.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
