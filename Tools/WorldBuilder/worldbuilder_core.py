"""Data model, validation, history, and headless previews for Ratna World Builder.

The Unity runtime reads the existing Version/Landmasses/Sites/Roads fields with
JsonUtility.  Editor-only information lives under ``_WorldBuilder``; Unity ignores
that unknown property, so saving this document does not require a runtime schema
change.
"""

from __future__ import annotations

import copy
import datetime as _datetime
import html
import json
import math
import os
from pathlib import Path
import re
import shutil
import struct
import tempfile
from dataclasses import dataclass
from typing import Any, Callable, Iterable, Optional
import zlib


EDITOR_KEY = "_WorldBuilder"
EDITOR_VERSION = 1
SITE_KINDS = ("city", "poi", "gate", "story_spawn")
EDITOR_ONLY_KINDS = frozenset(("gate", "story_spawn"))
KNOWN_BIOMES = ("Halbrand", "Sarrakh", "IslandGreen", "IslandRock")
ID_PATTERN = re.compile(r"^[a-z][a-z0-9_.-]*$")
DEFAULT_WORLD = (
    Path(__file__).resolve().parents[2]
    / "Assets"
    / "Resources"
    / "Data"
    / "World"
    / "kessil.world.json"
)
BACKUP_ROOT = Path(__file__).resolve().parents[2] / "WorldBuilderBackups"


@dataclass(frozen=True)
class Issue:
    severity: str
    code: str
    message: str
    subject: str = "world"

    def __str__(self) -> str:
        return f"{self.severity.upper()}: {self.message}"


def _point(x: float = 0.0, y: float = 0.0, z: float = 0.0) -> dict[str, float]:
    return {"x": float(x), "y": float(y), "z": float(z)}


def _finite(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def _number(value: Any, default: float = 0.0) -> float:
    return float(value) if _finite(value) else default


def _safe_id(text: str, fallback: str) -> str:
    candidate = re.sub(r"[^a-z0-9_.-]+", "_", (text or "").strip().lower()).strip("_.-")
    if not candidate or not candidate[0].isalpha():
        candidate = fallback
    return candidate


def _deepcopy(value: Any) -> Any:
    return copy.deepcopy(value)


def editor_metadata(data: dict[str, Any], create: bool = True) -> dict[str, Any]:
    raw = data.get(EDITOR_KEY)
    if isinstance(raw, dict):
        return raw
    if not create:
        return {}
    raw = {}
    data[EDITOR_KEY] = raw
    return raw


def ensure_editor_metadata(data: dict[str, Any]) -> dict[str, Any]:
    meta = editor_metadata(data)
    meta["Version"] = EDITOR_VERSION

    roads = data.get("Roads") if isinstance(data.get("Roads"), list) else []
    old_ids = meta.get("RoadIds") if isinstance(meta.get("RoadIds"), list) else []
    road_ids: list[str] = []
    used: set[str] = set()
    for index in range(len(roads)):
        raw = old_ids[index] if index < len(old_ids) and isinstance(old_ids[index], str) else ""
        road_id = _safe_id(raw, f"road_{index + 1:03d}")
        base = road_id
        suffix = 2
        while road_id in used:
            road_id = f"{base}_{suffix}"
            suffix += 1
        road_ids.append(road_id)
        used.add(road_id)
    meta["RoadIds"] = road_ids

    raw_kinds = meta.get("SiteKinds") if isinstance(meta.get("SiteKinds"), dict) else {}
    site_kinds: dict[str, str] = {}
    sites = data.get("Sites") if isinstance(data.get("Sites"), list) else []
    for site in sites:
        if not isinstance(site, dict):
            continue
        site_id = site.get("Id")
        if not isinstance(site_id, str) or not site_id:
            continue
        requested = raw_kinds.get(site_id)
        if requested not in ("city", "poi"):
            requested = "city" if site.get("IsCity") is True else "poi"
        site_kinds[site_id] = requested
    meta["SiteKinds"] = site_kinds

    if not isinstance(meta.get("Markers"), list):
        meta["Markers"] = []
    return meta


def world_bounds(data: dict[str, Any]) -> tuple[float, float, float, float]:
    min_x = _number(data.get("MapMinX"), -3200.0)
    max_x = _number(data.get("MapMaxX"), 3200.0)
    min_z = _number(data.get("MapMinZ"), -3650.0)
    max_z = _number(data.get("MapMaxZ"), 3750.0)
    if max_x <= min_x:
        min_x, max_x = -3200.0, 3200.0
    if max_z <= min_z:
        min_z, max_z = -3650.0, 3750.0
    return min_x, max_x, min_z, max_z


def coast_contains(data: dict[str, Any], point: dict[str, Any]) -> bool:
    x = _number(point.get("x"))
    z = _number(point.get("z"))
    coast_factor = _number(data.get("TerrainHalfExtent"), 0.49)
    for land in data.get("Landmasses", []):
        if not isinstance(land, dict):
            continue
        center = land.get("Center", {})
        size = land.get("Size", {})
        radius_x = _number(size.get("x")) * coast_factor
        radius_z = _number(size.get("z")) * coast_factor
        if radius_x <= 0.0 or radius_z <= 0.0:
            continue
        dx = (x - _number(center.get("x"))) / radius_x
        dz = (z - _number(center.get("z"))) / radius_z
        if dx * dx + dz * dz <= 1.0 + 1e-7:
            return True
    return False


def _check_point(issues: list[Issue], point: Any, subject: str, label: str) -> bool:
    if not isinstance(point, dict):
        issues.append(Issue("error", "point.missing", f"{label} is missing its x/y/z position.", subject))
        return False
    okay = True
    for axis in ("x", "y", "z"):
        if not _finite(point.get(axis)):
            issues.append(
                Issue("error", "point.not_number", f"{label} has an invalid {axis.upper()} coordinate.", subject)
            )
            okay = False
    return okay


def validate_world(data: Any) -> list[Issue]:
    issues: list[Issue] = []
    if not isinstance(data, dict):
        return [Issue("error", "world.not_object", "The world file must contain one JSON object.")]

    if data.get("Version") != 1:
        issues.append(Issue("error", "world.version", "Unity currently requires world Version 1."))

    water = data.get("WaterLevel")
    if not _finite(water):
        issues.append(Issue("error", "world.water", "WaterLevel must be a finite number."))
        water = 0.0
    water = _number(water)

    for key in ("VoidCatcherY", "OceanSize", "CameraFarPlane", "CausewayDeckY", "SafeZoneRadius"):
        value = data.get(key)
        if not _finite(value):
            issues.append(Issue("error", "world.scalar", f"{key} must be a finite number."))
    for key in ("OceanSize", "CameraFarPlane", "SafeZoneRadius"):
        if _finite(data.get(key)) and _number(data.get(key)) <= 0.0:
            issues.append(Issue("error", "world.positive", f"{key} must be greater than zero."))
    if _finite(data.get("CausewayDeckY")) and _number(data.get("CausewayDeckY")) <= water:
        issues.append(Issue("error", "world.causeway", "CausewayDeckY must sit above WaterLevel."))

    raw_bounds = tuple(data.get(key) for key in ("MapMinX", "MapMaxX", "MapMinZ", "MapMaxZ"))
    min_x, max_x, min_z, max_z = world_bounds(data)
    if not all(_finite(value) for value in raw_bounds):
        issues.append(Issue("error", "world.bounds", "Map bounds must all be finite numbers."))
    elif _number(raw_bounds[1]) <= _number(raw_bounds[0]) or _number(raw_bounds[3]) <= _number(raw_bounds[2]):
        issues.append(Issue("error", "world.bounds_order", "Map maximums must be greater than map minimums."))
    if _finite(data.get("OceanSize")):
        required_ocean = 2.0 * max(abs(value) for value in (min_x, max_x, min_z, max_z))
        if _number(data.get("OceanSize")) < required_ocean:
            issues.append(Issue("error", "world.ocean_bounds", "OceanSize must cover the full authored map bounds."))

    landmasses = data.get("Landmasses")
    if not isinstance(landmasses, list) or not landmasses:
        issues.append(Issue("error", "land.none", "Add at least one landmass before exporting."))
        landmasses = []

    land_ids: set[str] = set()
    padding_raw = data.get("MapExtentPadding")
    padding = _number(padding_raw, 0.0)
    if not _finite(padding_raw) or padding < 0.0:
        issues.append(Issue("error", "world.padding", "MapExtentPadding must be finite and non-negative."))
    coast_factor_raw = data.get("TerrainHalfExtent")
    coast_factor = _number(coast_factor_raw, 0.49)
    if not _finite(coast_factor_raw) or coast_factor <= 0.0 or coast_factor > 0.5:
        issues.append(
            Issue("error", "land.coast_factor", "TerrainHalfExtent must be greater than 0 and at most 0.5.")
        )

    terrain_seeds: set[int] = set()
    land_city_ids: set[str] = set()
    for index, land in enumerate(landmasses):
        subject = f"landmass[{index}]"
        if not isinstance(land, dict):
            issues.append(Issue("error", "land.not_object", f"Landmass {index + 1} is not a JSON object.", subject))
            continue
        name = land.get("Name")
        if not isinstance(name, str) or not name.strip():
            issues.append(Issue("error", "land.id_missing", f"Landmass {index + 1} needs a stable Name ID.", subject))
        elif name in land_ids:
            issues.append(Issue("error", "land.id_duplicate", f"Landmass ID '{name}' is used more than once.", subject))
        else:
            land_ids.add(name)
        biome = land.get("Biome")
        if not isinstance(biome, str) or not biome.strip():
            issues.append(Issue("error", "land.biome", f"Landmass '{name or index + 1}' needs a biome.", subject))
        elif biome not in KNOWN_BIOMES:
            issues.append(
                Issue("error", "land.biome_unknown",
                      f"Landmass '{name or index + 1}' uses unknown biome '{biome}'. Choose {', '.join(KNOWN_BIOMES)}.",
                      subject)
            )
        prop_count = land.get("PropCount")
        if not isinstance(prop_count, int) or isinstance(prop_count, bool) or prop_count < 0:
            issues.append(Issue("error", "land.props", f"Landmass '{name or index + 1}' PropCount must be a non-negative integer.", subject))
        seed = land.get("TerrainSeed")
        if not isinstance(seed, int) or isinstance(seed, bool) or seed == 0:
            issues.append(Issue("error", "land.seed", f"Landmass '{name or index + 1}' needs a non-zero integer TerrainSeed.", subject))
        elif seed in terrain_seeds:
            issues.append(Issue("error", "land.seed_duplicate", f"TerrainSeed {seed} is used more than once.", subject))
        else:
            terrain_seeds.add(seed)
        city_id = land.get("CityId")
        city_name = land.get("CityName")
        if city_id not in (None, ""):
            if not isinstance(city_id, str) or not ID_PATTERN.fullmatch(city_id):
                issues.append(Issue("error", "land.city_id", f"Landmass '{name or index + 1}' has invalid CityId '{city_id}'.", subject))
            elif city_id in land_city_ids:
                issues.append(Issue("error", "land.city_duplicate", f"CityId '{city_id}' is assigned to more than one landmass.", subject))
            else:
                land_city_ids.add(city_id)
            if not isinstance(city_name, str) or not city_name.strip():
                issues.append(Issue("error", "land.city_name", f"City landmass '{name or index + 1}' needs a CityName.", subject))
        elif isinstance(city_name, str) and city_name.strip():
            issues.append(Issue("error", "land.city_id_missing", f"Landmass '{name or index + 1}' has CityName '{city_name}' but no stable CityId.", subject))
        center = land.get("Center")
        size = land.get("Size")
        center_ok = _check_point(issues, center, subject, f"Landmass '{name or index + 1}' centre")
        size_ok = _check_point(issues, size, subject, f"Landmass '{name or index + 1}' size")
        if size_ok:
            for axis, friendly in (("x", "width"), ("y", "elevation/relief"), ("z", "depth")):
                if _number(size.get(axis)) <= 0.0:
                    issues.append(
                        Issue("error", "land.size", f"Landmass '{name or index + 1}' {friendly} must be greater than zero.", subject)
                    )
        if center_ok and size_ok:
            # The Unity preflight reserves the full authored rectangle, not only the
            # slightly inset elliptical mesh. Matching it here prevents a save that the
            # one-button production preview immediately rejects.
            west = _number(center.get("x")) - _number(size.get("x")) * 0.5
            east = _number(center.get("x")) + _number(size.get("x")) * 0.5
            south = _number(center.get("z")) - _number(size.get("z")) * 0.5
            north = _number(center.get("z")) + _number(size.get("z")) * 0.5
            if west < min_x + padding or east > max_x - padding or south < min_z + padding or north > max_z - padding:
                issues.append(
                    Issue(
                        "error",
                        "land.outside_map",
                        f"Landmass '{name or index + 1}' crosses the map edge or its {padding:g} m safety margin.",
                        subject,
                    )
                )

    sites = data.get("Sites")
    if not isinstance(sites, list) or not sites:
        issues.append(Issue("error", "site.none", "Add at least one runtime site before exporting."))
        sites = []

    meta = editor_metadata(data, create=False)
    raw_markers = meta.get("Markers", []) if isinstance(meta, dict) else []
    markers = raw_markers if isinstance(raw_markers, list) else []
    raw_kinds = meta.get("SiteKinds", {}) if isinstance(meta, dict) else {}
    site_kinds = raw_kinds if isinstance(raw_kinds, dict) else {}
    stable_ids: set[str] = set()
    site_city_ids: set[str] = set()

    def validate_site(site: Any, index: int, editor_only: bool) -> None:
        prefix = "marker" if editor_only else "site"
        subject = f"{prefix}[{index}]"
        if not isinstance(site, dict):
            issues.append(Issue("error", f"{prefix}.not_object", f"{prefix.title()} {index + 1} is not a JSON object.", subject))
            return
        site_id = site.get("Id")
        if not isinstance(site_id, str) or not site_id:
            issues.append(Issue("error", f"{prefix}.id_missing", f"{prefix.title()} {index + 1} needs a stable ID.", subject))
            site_id = f"{prefix} {index + 1}"
        elif not ID_PATTERN.fullmatch(site_id):
            issues.append(
                Issue(
                    "error",
                    f"{prefix}.id_format",
                    f"ID '{site_id}' must start with a letter and use only lowercase letters, numbers, '_', '-' or '.'.",
                    subject,
                )
            )
        if site_id in stable_ids:
            issues.append(Issue("error", f"{prefix}.id_duplicate", f"Stable ID '{site_id}' is used more than once.", subject))
        stable_ids.add(site_id)

        if not isinstance(site.get("DisplayName"), str) or not site.get("DisplayName", "").strip():
            issues.append(Issue("error", f"{prefix}.display_name", f"'{site_id}' needs a display name.", subject))
        kind = site.get("Kind") if editor_only else site_kinds.get(site_id, "city" if site.get("IsCity") is True else "poi")
        if not editor_only:
            if not isinstance(site.get("IsCity"), bool):
                issues.append(Issue("error", "site.is_city", f"'{site_id}' IsCity must be true or false.", subject))
            radius = site.get("DiscoverRadius")
            if not _finite(radius) or _number(radius) <= 0.0:
                issues.append(Issue("error", "site.radius", f"'{site_id}' DiscoverRadius must be positive.", subject))
            if site.get("IsCity") is True and isinstance(site_id, str):
                site_city_ids.add(site_id)
            expected_kind = "city" if site.get("IsCity") is True else "poi"
            if kind != expected_kind:
                issues.append(Issue("error", "site.kind_mismatch", f"'{site_id}' metadata kind '{kind}' disagrees with IsCity={site.get('IsCity')!r}.", subject))

        if kind not in SITE_KINDS:
            issues.append(
                Issue("error", f"{prefix}.kind", f"'{site_id}' has unknown kind '{kind}'. Choose city, poi, gate or story_spawn.", subject)
            )
        if editor_only and kind not in EDITOR_ONLY_KINDS:
            issues.append(
                Issue("error", "marker.runtime_kind", f"Editor marker '{site_id}' must use gate or story_spawn kind.", subject)
            )
        if not editor_only and kind in EDITOR_ONLY_KINDS:
            issues.append(
                Issue(
                    "error",
                    "site.editor_kind",
                    f"Runtime site '{site_id}' uses editor-only kind '{kind}'. Recreate it as an editor marker.",
                    subject,
                )
            )

        world_position = site.get("WorldPosition")
        world_ok = _check_point(issues, world_position, subject, f"'{site_id}' world position")
        travel_position = world_position if editor_only else site.get("TravelPosition")
        travel_ok = _check_point(issues, travel_position, subject, f"'{site_id}' travel/spawn position")
        for label, point, okay in (("world marker", world_position, world_ok), ("spawn", travel_position, travel_ok)):
            if not okay:
                continue
            if _number(point.get("y")) <= water:
                issues.append(
                    Issue(
                        "error",
                        f"{prefix}.underwater",
                        f"'{site_id}' {label} is at Y {_number(point.get('y')):g}, at or below water Y {water:g}.",
                        subject,
                    )
                )
            if not coast_contains(data, point):
                issues.append(
                    Issue("error", f"{prefix}.off_land", f"'{site_id}' {label} is over open water. Move it inside a coastline.", subject)
                )

    for index, site in enumerate(sites):
        validate_site(site, index, False)
    for index, marker in enumerate(markers):
        validate_site(marker, index, True)

    for city_id in sorted(land_city_ids - site_city_ids):
        issues.append(Issue("error", "city.site_missing", f"City landmass '{city_id}' has no matching city site."))
    for city_id in sorted(site_city_ids - land_city_ids):
        issues.append(Issue("error", "city.land_missing", f"City site '{city_id}' has no matching city landmass."))

    for key in ("CaldemarSpawnPad", "BanditCamp", "CoastalRuin", "SafeZoneCenter"):
        point = data.get(key)
        if _check_point(issues, point, "world", key) and key == "CaldemarSpawnPad":
            if _number(point.get("y")) <= water:
                issues.append(Issue("error", "world.spawn_underwater", "The player spawn is at or below water."))
            if not coast_contains(data, point):
                issues.append(Issue("error", "world.spawn_off_land", "The player spawn is over open water."))

    roads = data.get("Roads")
    if not isinstance(roads, list) or not roads:
        issues.append(Issue("error", "road.none", "Add at least one road before exporting."))
        roads = []
    road_ids = meta.get("RoadIds", []) if isinstance(meta, dict) else []
    if not isinstance(road_ids, list) or len(road_ids) != len(roads):
        issues.append(
            Issue("error", "road.ids", "Road stable IDs are missing or out of sync. Open and save the file in World Builder to repair them.")
        )
        road_ids = []
    used_road_ids: set[str] = set()
    for index, road in enumerate(roads):
        road_id = road_ids[index] if index < len(road_ids) else f"road {index + 1}"
        subject = f"road[{index}]"
        if not isinstance(road_id, str) or not ID_PATTERN.fullmatch(road_id):
            issues.append(
                Issue("error", "road.id_format", f"Road ID '{road_id}' is invalid; use a lowercase stable ID.", subject)
            )
        elif road_id in used_road_ids:
            issues.append(Issue("error", "road.id_duplicate", f"Road ID '{road_id}' is used more than once.", subject))
        used_road_ids.add(road_id)

        if not isinstance(road, dict):
            issues.append(Issue("error", "road.not_object", f"Road '{road_id}' is not a JSON object.", subject))
            continue
        points = road.get("Points")
        if not isinstance(points, list) or len(points) < 2:
            issues.append(Issue("error", "road.short", f"Road '{road_id}' needs at least two points.", subject))
            continue
        point_states = [_check_point(issues, point, subject, f"Road '{road_id}' point {i + 1}") for i, point in enumerate(points)]
        for point_index in range(1, len(points)):
            if not point_states[point_index - 1] or not point_states[point_index]:
                continue
            first, second = points[point_index - 1], points[point_index]
            distance = math.hypot(_number(first.get("x")) - _number(second.get("x")), _number(first.get("z")) - _number(second.get("z")))
            if distance < 0.1:
                issues.append(
                    Issue(
                        "error",
                        "road.duplicate_point",
                        f"Road '{road_id}' points {point_index} and {point_index + 1} overlap. Move or remove one.",
                        subject,
                    )
                )
        if point_states[0] and not coast_contains(data, points[0]):
            issues.append(Issue("error", "road.start_water", f"Road '{road_id}' starts over open water.", subject))
        if point_states[-1] and not coast_contains(data, points[-1]):
            issues.append(Issue("error", "road.end_water", f"Road '{road_id}' ends over open water.", subject))

    return issues


class WorldModel:
    """Mutable world document with coarse-grained undo/redo transactions."""

    def __init__(self, data: dict[str, Any], source_path: Optional[Path] = None):
        if not isinstance(data, dict):
            raise ValueError("World JSON must contain an object.")
        self.data = _deepcopy(data)
        ensure_editor_metadata(self.data)
        self.source_path = Path(source_path).resolve() if source_path else None
        self.dirty = False
        self._undo: list[tuple[str, dict[str, Any]]] = []
        self._redo: list[tuple[str, dict[str, Any]]] = []

    @classmethod
    def load(cls, path: os.PathLike[str] | str = DEFAULT_WORLD) -> "WorldModel":
        source = Path(path).resolve()
        with source.open("r", encoding="utf-8-sig") as handle:
            data = json.load(handle)
        return cls(data, source)

    def snapshot(self) -> dict[str, Any]:
        return _deepcopy(self.data)

    def commit(self, label: str, before: dict[str, Any]) -> bool:
        ensure_editor_metadata(self.data)
        if before == self.data:
            return False
        self._undo.append((label, before))
        if len(self._undo) > 100:
            self._undo.pop(0)
        self._redo.clear()
        self.dirty = True
        return True

    def change(self, label: str, operation: Callable[[dict[str, Any]], None]) -> bool:
        before = self.snapshot()
        operation(self.data)
        return self.commit(label, before)

    @property
    def can_undo(self) -> bool:
        return bool(self._undo)

    @property
    def can_redo(self) -> bool:
        return bool(self._redo)

    @property
    def undo_label(self) -> str:
        return self._undo[-1][0] if self._undo else ""

    @property
    def redo_label(self) -> str:
        return self._redo[-1][0] if self._redo else ""

    def undo(self) -> bool:
        if not self._undo:
            return False
        label, prior = self._undo.pop()
        self._redo.append((label, self.snapshot()))
        self.data = prior
        self.dirty = True
        return True

    def redo(self) -> bool:
        if not self._redo:
            return False
        label, future = self._redo.pop()
        self._undo.append((label, self.snapshot()))
        self.data = future
        self.dirty = True
        return True

    def road_id(self, index: int) -> str:
        return ensure_editor_metadata(self.data)["RoadIds"][index]

    def site_kind(self, site: dict[str, Any], editor_only: bool = False) -> str:
        if editor_only:
            return str(site.get("Kind", "story_spawn"))
        return str(
            ensure_editor_metadata(self.data)["SiteKinds"].get(
                site.get("Id"), "city" if site.get("IsCity") is True else "poi"
            )
        )

    def add_landmass(self, x: float, z: float) -> int:
        index = len(self.data.setdefault("Landmasses", []))
        used = {land.get("Name") for land in self.data["Landmasses"] if isinstance(land, dict)}
        name = f"Landmass_{index + 1:02d}"
        suffix = index + 1
        while name in used:
            suffix += 1
            name = f"Landmass_{suffix:02d}"

        def operation(data: dict[str, Any]) -> None:
            data["Landmasses"].append(
                {
                    "Name": name,
                    "Biome": "Halbrand",
                    "Center": _point(x, 0.0, z),
                    "Size": _point(700.0, 24.0, 550.0),
                    "PropCount": 60,
                    "TerrainSeed": 5000 + suffix,
                }
            )

        self.change(f"Add {name}", operation)
        return index

    def add_road(self, points: Optional[list[dict[str, float]]] = None) -> int:
        roads = self.data.setdefault("Roads", [])
        index = len(roads)

        def operation(data: dict[str, Any]) -> None:
            data["Roads"].append({"Points": _deepcopy(points or [])})
            ensure_editor_metadata(data)

        self.change("Add road", operation)
        return index

    def add_site(self, kind: str, x: float, y: float, z: float) -> tuple[str, int]:
        if kind not in SITE_KINDS:
            raise ValueError(f"Unknown site kind: {kind}")
        meta = ensure_editor_metadata(self.data)
        all_ids = {
            item.get("Id")
            for item in list(self.data.get("Sites", [])) + list(meta.get("Markers", []))
            if isinstance(item, dict)
        }
        stem = "story_spawn" if kind == "story_spawn" else kind
        suffix = 1
        site_id = f"{stem}_{suffix:02d}"
        while site_id in all_ids:
            suffix += 1
            site_id = f"{stem}_{suffix:02d}"
        display_name = site_id.replace("_", " ").title()
        if kind in EDITOR_ONLY_KINDS:
            index = len(meta["Markers"])

            def operation(data: dict[str, Any]) -> None:
                ensure_editor_metadata(data)["Markers"].append(
                    {"Id": site_id, "DisplayName": display_name, "Kind": kind, "WorldPosition": _point(x, y, z)}
                )

            self.change(f"Add {kind.replace('_', ' ')}", operation)
            return "marker", index

        index = len(self.data.setdefault("Sites", []))

        def operation(data: dict[str, Any]) -> None:
            data["Sites"].append(
                {
                    "Id": site_id,
                    "DisplayName": display_name,
                    "IsCity": kind == "city",
                    "DiscoverRadius": 280.0 if kind == "city" else 90.0,
                    "WorldPosition": _point(x, y, z),
                    "TravelPosition": _point(x, y + 1.2, z),
                }
            )
            ensure_editor_metadata(data)["SiteKinds"][site_id] = kind

        self.change(f"Add {kind}", operation)
        return "site", index

    def delete(self, kind: str, index: int) -> bool:
        def operation(data: dict[str, Any]) -> None:
            meta = ensure_editor_metadata(data)
            if kind == "landmass":
                del data["Landmasses"][index]
            elif kind == "road":
                del data["Roads"][index]
                del meta["RoadIds"][index]
            elif kind == "site":
                site = data["Sites"].pop(index)
                meta["SiteKinds"].pop(site.get("Id"), None)
            elif kind == "marker":
                del meta["Markers"][index]
            else:
                raise ValueError(kind)

        try:
            return self.change(f"Delete {kind}", operation)
        except (IndexError, KeyError):
            return False

    def save(self, path: Optional[os.PathLike[str] | str] = None, require_valid: bool = True) -> Optional[Path]:
        target = Path(path).resolve() if path else self.source_path
        if target is None:
            raise ValueError("Choose a destination before saving.")
        ensure_editor_metadata(self.data)
        errors = [issue for issue in validate_world(self.data) if issue.severity == "error"]
        if require_valid and errors:
            raise ValueError("Cannot save an invalid world:\n" + "\n".join(f"- {item.message}" for item in errors))

        target.parent.mkdir(parents=True, exist_ok=True)
        backup: Optional[Path] = None
        if target.exists():
            stamp = _datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
            BACKUP_ROOT.mkdir(parents=True, exist_ok=True)
            backup = BACKUP_ROOT / f"{target.name}.{stamp}.bak"
            shutil.copy2(target, backup)

        serialized = json.dumps(self.data, indent=2, ensure_ascii=False) + "\n"
        handle = tempfile.NamedTemporaryFile("w", encoding="utf-8", newline="\n", dir=target.parent, delete=False)
        temporary = Path(handle.name)
        try:
            with handle:
                handle.write(serialized)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary, target)
        finally:
            if temporary.exists():
                temporary.unlink()
        self.source_path = target
        self.dirty = False
        return backup


BIOME_COLORS: dict[str, tuple[int, int, int]] = {
    "Halbrand": (116, 133, 92),
    "Sarrakh": (169, 124, 73),
    "IslandGreen": (94, 138, 103),
    "IslandRock": (119, 116, 110),
}
OCEAN = (20, 43, 61)
GRID = (40, 64, 78)
ROAD = (219, 179, 100)
SITE = (235, 222, 174)
CITY = (226, 151, 69)
MARKER = (176, 119, 187)


class MapTransform:
    def __init__(self, data: dict[str, Any], width: int, height: int, margin: int = 32):
        self.min_x, self.max_x, self.min_z, self.max_z = world_bounds(data)
        self.width = width
        self.height = height
        self.margin = margin
        span_x = self.max_x - self.min_x
        span_z = self.max_z - self.min_z
        self.scale = min((width - margin * 2) / span_x, (height - margin * 2) / span_z)
        self.offset_x = (width - span_x * self.scale) * 0.5
        self.offset_y = (height - span_z * self.scale) * 0.5

    def world_to_screen(self, x: float, z: float) -> tuple[float, float]:
        sx = self.offset_x + (x - self.min_x) * self.scale
        sy = self.height - (self.offset_y + (z - self.min_z) * self.scale)
        return sx, sy

    def screen_to_world(self, sx: float, sy: float) -> tuple[float, float]:
        x = (sx - self.offset_x) / self.scale + self.min_x
        z = ((self.height - sy) - self.offset_y) / self.scale + self.min_z
        return x, z


def _preview_shapes(data: dict[str, Any], width: int, height: int) -> tuple[MapTransform, list[dict[str, Any]]]:
    transform = MapTransform(data, width, height)
    shapes: list[dict[str, Any]] = []
    coast_factor = _number(data.get("TerrainHalfExtent"), 0.49)
    for land in data.get("Landmasses", []):
        if not isinstance(land, dict):
            continue
        center, size = land.get("Center", {}), land.get("Size", {})
        cx, cy = transform.world_to_screen(_number(center.get("x")), _number(center.get("z")))
        rx = _number(size.get("x")) * coast_factor * transform.scale
        ry = _number(size.get("z")) * coast_factor * transform.scale
        shapes.append(
            {
                "type": "ellipse",
                "box": (cx - rx, cy - ry, cx + rx, cy + ry),
                "fill": BIOME_COLORS.get(str(land.get("Biome")), (115, 119, 105)),
                "label": str(land.get("CityName") or land.get("Name") or "Landmass"),
                "elevation": _number(size.get("y")),
            }
        )
    meta = editor_metadata(data, create=False)
    road_ids = meta.get("RoadIds", []) if isinstance(meta.get("RoadIds", []), list) else []
    for index, road in enumerate(data.get("Roads", [])):
        if not isinstance(road, dict):
            continue
        points = [
            transform.world_to_screen(_number(point.get("x")), _number(point.get("z")))
            for point in road.get("Points", [])
            if isinstance(point, dict)
        ]
        shapes.append(
            {"type": "polyline", "points": points, "color": ROAD, "label": road_ids[index] if index < len(road_ids) else f"road_{index + 1:03d}"}
        )
    site_kinds = meta.get("SiteKinds", {}) if isinstance(meta.get("SiteKinds", {}), dict) else {}
    for site in data.get("Sites", []):
        if not isinstance(site, dict):
            continue
        position = site.get("WorldPosition", {})
        x, y = transform.world_to_screen(_number(position.get("x")), _number(position.get("z")))
        kind = site_kinds.get(site.get("Id"), "city" if site.get("IsCity") is True else "poi")
        shapes.append(
            {
                "type": "site",
                "position": (x, y),
                "fill": CITY if kind == "city" else SITE,
                "label": str(site.get("DisplayName") or site.get("Id") or "site"),
                "kind": kind,
            }
        )
    markers = meta.get("Markers", []) if isinstance(meta.get("Markers", []), list) else []
    for marker in markers:
        if not isinstance(marker, dict):
            continue
        position = marker.get("WorldPosition", {})
        x, y = transform.world_to_screen(_number(position.get("x")), _number(position.get("z")))
        shapes.append(
            {
                "type": "site",
                "position": (x, y),
                "fill": MARKER,
                "label": str(marker.get("DisplayName") or marker.get("Id") or "marker"),
                "kind": str(marker.get("Kind", "marker")),
            }
        )
    return transform, shapes


def export_svg(data: dict[str, Any], destination: os.PathLike[str] | str, width: int = 1200, height: int = 900) -> Path:
    target = Path(destination)
    transform, shapes = _preview_shapes(data, width, height)
    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        f'<rect width="{width}" height="{height}" fill="rgb{OCEAN}"/>',
        '<g font-family="Segoe UI, sans-serif" font-size="13" fill="#f4e8c8">',
    ]
    for x in range(math.ceil(transform.min_x / 1000) * 1000, math.floor(transform.max_x / 1000) * 1000 + 1, 1000):
        sx, _ = transform.world_to_screen(x, 0)
        lines.append(f'<path d="M {sx:.1f} 0 V {height}" stroke="#28404e" stroke-width="1"/>')
    for z in range(math.ceil(transform.min_z / 1000) * 1000, math.floor(transform.max_z / 1000) * 1000 + 1, 1000):
        _, sy = transform.world_to_screen(0, z)
        lines.append(f'<path d="M 0 {sy:.1f} H {width}" stroke="#28404e" stroke-width="1"/>')
    for shape in shapes:
        if shape["type"] == "ellipse":
            x1, y1, x2, y2 = shape["box"]
            color = shape["fill"]
            cx, cy = (x1 + x2) * 0.5, (y1 + y2) * 0.5
            lines.append(
                f'<ellipse cx="{cx:.1f}" cy="{cy:.1f}" rx="{(x2 - x1) * .5:.1f}" ry="{(y2 - y1) * .5:.1f}" '
                f'fill="rgb{color}" stroke="#d6c69e" stroke-width="2"/>'
            )
            label = html.escape(shape["label"])
            lines.append(f'<text x="{cx:.1f}" y="{cy:.1f}" text-anchor="middle">{label} · {shape["elevation"]:g}m</text>')
        elif shape["type"] == "polyline" and len(shape["points"]) >= 2:
            points = " ".join(f"{x:.1f},{y:.1f}" for x, y in shape["points"])
            color = shape["color"]
            lines.append(f'<polyline points="{points}" fill="none" stroke="rgb{color}" stroke-width="5" stroke-linejoin="round"/>')
        elif shape["type"] == "site":
            x, y = shape["position"]
            color = shape["fill"]
            label = html.escape(f"{shape['label']} [{shape['kind']}]")
            lines.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="7" fill="rgb{color}" stroke="#201d19" stroke-width="2"/>')
            lines.append(f'<text x="{x + 10:.1f}" y="{y - 8:.1f}">{label}</text>')
    lines.extend(
        [
            f'<text x="24" y="32" font-size="24" font-weight="600">Ratna Bay world preview</text>',
            f'<text x="24" y="54" font-size="12">Bounds {transform.min_x:g}…{transform.max_x:g} X / {transform.min_z:g}…{transform.max_z:g} Z</text>',
            '</g>',
            '</svg>',
        ]
    )
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return target


class _Raster:
    def __init__(self, width: int, height: int, background: tuple[int, int, int]):
        self.width, self.height = width, height
        self.pixels = bytearray(background * (width * height))

    def set(self, x: int, y: int, color: tuple[int, int, int]) -> None:
        if 0 <= x < self.width and 0 <= y < self.height:
            offset = (y * self.width + x) * 3
            self.pixels[offset : offset + 3] = bytes(color)

    def line(self, x0: float, y0: float, x1: float, y1: float, color: tuple[int, int, int], width: int = 1) -> None:
        x0i, y0i, x1i, y1i = map(round, (x0, y0, x1, y1))
        dx, dy = abs(x1i - x0i), -abs(y1i - y0i)
        sx, sy = (1 if x0i < x1i else -1), (1 if y0i < y1i else -1)
        error = dx + dy
        radius = max(0, width // 2)
        while True:
            for oy in range(-radius, radius + 1):
                for ox in range(-radius, radius + 1):
                    self.set(x0i + ox, y0i + oy, color)
            if x0i == x1i and y0i == y1i:
                break
            doubled = 2 * error
            if doubled >= dy:
                error += dy
                x0i += sx
            if doubled <= dx:
                error += dx
                y0i += sy

    def ellipse(self, box: tuple[float, float, float, float], fill: tuple[int, int, int], outline: tuple[int, int, int]) -> None:
        x1, y1, x2, y2 = box
        cx, cy = (x1 + x2) * 0.5, (y1 + y2) * 0.5
        rx, ry = max(1.0, (x2 - x1) * 0.5), max(1.0, (y2 - y1) * 0.5)
        for y in range(max(0, math.floor(y1)), min(self.height, math.ceil(y2) + 1)):
            normalized = 1.0 - ((y - cy) / ry) ** 2
            if normalized < 0:
                continue
            half = rx * math.sqrt(normalized)
            left, right = max(0, math.ceil(cx - half)), min(self.width - 1, math.floor(cx + half))
            for x in range(left, right + 1):
                self.set(x, y, fill)
            self.set(left, y, outline)
            self.set(right, y, outline)

    def circle(self, cx: float, cy: float, radius: int, fill: tuple[int, int, int]) -> None:
        for y in range(round(cy) - radius, round(cy) + radius + 1):
            for x in range(round(cx) - radius, round(cx) + radius + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= radius * radius:
                    self.set(x, y, fill)

    def save_png(self, target: Path) -> None:
        raw = b"".join(b"\x00" + bytes(self.pixels[row * self.width * 3 : (row + 1) * self.width * 3]) for row in range(self.height))

        def chunk(kind: bytes, payload: bytes) -> bytes:
            return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)

        png = b"\x89PNG\r\n\x1a\n"
        png += chunk(b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 2, 0, 0, 0))
        png += chunk(b"IDAT", zlib.compress(raw, 9))
        png += chunk(b"IEND", b"")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(png)


def export_png(data: dict[str, Any], destination: os.PathLike[str] | str, width: int = 1200, height: int = 900) -> Path:
    target = Path(destination)
    transform, shapes = _preview_shapes(data, width, height)
    image = _Raster(width, height, OCEAN)
    for x in range(math.ceil(transform.min_x / 1000) * 1000, math.floor(transform.max_x / 1000) * 1000 + 1, 1000):
        sx, _ = transform.world_to_screen(x, 0)
        image.line(sx, 0, sx, height - 1, GRID)
    for z in range(math.ceil(transform.min_z / 1000) * 1000, math.floor(transform.max_z / 1000) * 1000 + 1, 1000):
        _, sy = transform.world_to_screen(0, z)
        image.line(0, sy, width - 1, sy, GRID)
    for shape in shapes:
        if shape["type"] == "ellipse":
            image.ellipse(shape["box"], shape["fill"], (213, 196, 151))
        elif shape["type"] == "polyline":
            for first, second in zip(shape["points"], shape["points"][1:]):
                image.line(*first, *second, shape["color"], 5)
        elif shape["type"] == "site":
            image.circle(*shape["position"], 7, shape["fill"])
    image.save_png(target)
    return target


def export_preview(data: dict[str, Any], destination: os.PathLike[str] | str, width: int = 1200, height: int = 900) -> Path:
    target = Path(destination)
    if target.suffix.lower() == ".svg":
        return export_svg(data, target, width, height)
    if target.suffix.lower() != ".png":
        raise ValueError("Preview filename must end in .png or .svg.")
    return export_png(data, target, width, height)


def format_issues(issues: Iterable[Issue]) -> str:
    values = list(issues)
    if not values:
        return "World is valid: landmasses, dry spawns, stable IDs and road endpoints all pass."
    return "\n".join(str(issue) for issue in values)
