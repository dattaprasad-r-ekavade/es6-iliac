import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from world_builder import (  # noqa: E402
    PROJECT_ROOT,
    UNITY_PREVIEW_METHOD,
    WorldBuilderApp,
    find_unity_executable,
    unity_preview_arguments,
)
from worldbuilder_core import DEFAULT_WORLD, WorldModel  # noqa: E402


class _Field:
    def __init__(self, value):
        self.value = str(value)

    def get(self):
        return self.value


class UnityPreviewLaunchTests(unittest.TestCase):
    def test_configured_unity_path_is_used_without_a_shell_command(self):
        with tempfile.TemporaryDirectory() as temporary:
            unity = Path(temporary) / "Unity.exe"
            unity.touch()
            with patch.dict(os.environ, {"RATNA_UNITY_PATH": str(unity)}):
                self.assertEqual(unity.resolve(), find_unity_executable())

    def test_preview_arguments_target_the_project_and_sanctioned_bridge(self):
        unity = Path(r"C:\Unity\Unity.exe")
        log = PROJECT_ROOT / "Temp" / "worldbuilder-preview.log"
        arguments = unity_preview_arguments(unity, log)

        self.assertEqual(str(unity), arguments[0])
        self.assertIn(str(PROJECT_ROOT), arguments)
        self.assertIn(UNITY_PREVIEW_METHOD, arguments)
        self.assertEqual(str(log), arguments[-1])
        self.assertNotIn("cmd", arguments)
        self.assertNotIn("powershell", arguments)

    def test_typed_property_is_committed_before_save_or_selection_can_replace_widgets(self):
        app = WorldBuilderApp.__new__(WorldBuilderApp)
        app.model = WorldModel.load(DEFAULT_WORLD)
        app.selection = ("landmass", 0)
        app.road_point = None
        land = app.model.data["Landmasses"][0]
        app.fields = {
            "Name": _Field(land["Name"]),
            "Biome": _Field(land["Biome"]),
            "CityId": _Field(land.get("CityId", "")),
            "CityName": _Field(land.get("CityName", "")),
            "Center.x": _Field(321.0),
            "Center.y": _Field(land["Center"]["y"]),
            "Center.z": _Field(land["Center"]["z"]),
            "Size.x": _Field(land["Size"]["x"]),
            "Size.y": _Field(land["Size"]["y"]),
            "Size.z": _Field(land["Size"]["z"]),
            "PropCount": _Field(land["PropCount"]),
            "TerrainSeed": _Field(land["TerrainSeed"]),
        }
        app.properties_pending = True

        self.assertTrue(app.commit_pending_properties())
        self.assertEqual(321.0, app.model.data["Landmasses"][0]["Center"]["x"])
        self.assertTrue(app.model.dirty)
        self.assertFalse(app.properties_pending)


if __name__ == "__main__":
    unittest.main()
