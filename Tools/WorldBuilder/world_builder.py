#!/usr/bin/env python3
"""Ratna World Builder: a small, dependency-free visual editor for kessil.world.json."""

from __future__ import annotations

import argparse
import math
import os
from pathlib import Path
import subprocess
import sys
import time
from typing import Any, Optional

from worldbuilder_core import (
    BIOME_COLORS,
    CITY,
    DEFAULT_WORLD,
    EDITOR_ONLY_KINDS,
    GRID,
    MARKER,
    OCEAN,
    ROAD,
    SITE,
    SITE_KINDS,
    MapTransform,
    WorldModel,
    coast_contains,
    ensure_editor_metadata,
    export_preview,
    format_issues,
    validate_world,
)


APP_TITLE = "Ratna World Builder"
PROJECT_ROOT = Path(__file__).resolve().parents[2]
UNITY_PREVIEW_METHOD = "WorldBuilderPreviewCommand.BuildValidateAndCapture"


def find_unity_executable() -> Optional[Path]:
    configured = os.environ.get("RATNA_UNITY_PATH")
    if configured:
        candidate = Path(configured).expanduser()
        if candidate.is_file():
            return candidate.resolve()

    version_file = PROJECT_ROOT / "ProjectSettings" / "ProjectVersion.txt"
    version = "6000.5.3f1"
    try:
        for line in version_file.read_text(encoding="utf-8").splitlines():
            if line.startswith("m_EditorVersion:"):
                version = line.split(":", 1)[1].strip()
                break
    except OSError:
        pass

    roots = [
        Path(os.environ.get("PROGRAMFILES", r"C:\Program Files")),
        Path(os.environ.get("PROGRAMFILES(X86)", r"C:\Program Files (x86)")),
    ]
    for root in roots:
        candidate = root / "Unity" / "Hub" / "Editor" / version / "Editor" / "Unity.exe"
        if candidate.is_file():
            return candidate.resolve()
    return None


def unity_preview_arguments(unity: Path, log_path: Path) -> list[str]:
    return [
        str(unity), "-batchmode", "-quit", "-projectPath", str(PROJECT_ROOT),
        "-executeMethod", UNITY_PREVIEW_METHOD, "-logFile", str(log_path),
    ]


def _rgb(color: tuple[int, int, int]) -> str:
    return "#%02x%02x%02x" % color


def _distance_to_segment(
    px: float, py: float, ax: float, ay: float, bx: float, by: float
) -> float:
    dx, dy = bx - ax, by - ay
    if dx == 0 and dy == 0:
        return math.hypot(px - ax, py - ay)
    amount = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - (ax + amount * dx), py - (ay + amount * dy))


class WorldBuilderApp:
    def __init__(self, root: Any, world_path: Path):
        import tkinter as tk
        from tkinter import ttk

        self.tk, self.ttk = tk, ttk
        self.root = root
        self.model = WorldModel.load(world_path)
        self.selection: Optional[tuple[str, int]] = None
        self.road_point: Optional[int] = None
        self.mode = "select"
        self.transform: Optional[MapTransform] = None
        self.drag_before: Optional[dict[str, Any]] = None
        self.drag_label = "Move item"
        self.drag_dirty_before = False
        self.drag_anchor: Optional[tuple[float, float]] = None
        self.drag_original: Optional[dict[str, Any]] = None
        self.drawing_road: Optional[int] = None
        self.drawing_before: Optional[dict[str, Any]] = None
        self.drawing_dirty_before = False
        self.fields: dict[str, Any] = {}
        self.properties_pending = False
        self.unity_preview_process: Optional[subprocess.Popen[Any]] = None
        self.unity_preview_started_ns = 0

        root.title(APP_TITLE)
        root.geometry("1380x860")
        root.minsize(980, 650)
        root.protocol("WM_DELETE_WINDOW", self.close)

        style = ttk.Style()
        if "clam" in style.theme_names():
            style.theme_use("clam")
        style.configure("Accent.TButton", font=("Segoe UI", 9, "bold"))

        self._make_menu()
        self._make_toolbar()
        self._make_workspace()
        self._bind_shortcuts()
        self.refresh_all()

    # ----- layout ---------------------------------------------------------
    def _make_menu(self) -> None:
        tk = self.tk
        menu = tk.Menu(self.root)
        file_menu = tk.Menu(menu, tearoff=False)
        file_menu.add_command(label="Open...", accelerator="Ctrl+O", command=self.open_file)
        file_menu.add_separator()
        file_menu.add_command(label="Save", accelerator="Ctrl+S", command=self.save)
        file_menu.add_command(label="Save As...", accelerator="Ctrl+Shift+S", command=self.save_as)
        file_menu.add_separator()
        file_menu.add_command(label="Export PNG...", command=lambda: self.export(".png"))
        file_menu.add_command(label="Export SVG...", command=lambda: self.export(".svg"))
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self.close)
        menu.add_cascade(label="File", menu=file_menu)

        edit_menu = tk.Menu(menu, tearoff=False)
        edit_menu.add_command(label="Undo", accelerator="Ctrl+Z", command=self.undo)
        edit_menu.add_command(label="Redo", accelerator="Ctrl+Y", command=self.redo)
        edit_menu.add_separator()
        edit_menu.add_command(label="Delete selected", accelerator="Delete", command=self.delete_selected)
        menu.add_cascade(label="Edit", menu=edit_menu)

        world_menu = tk.Menu(menu, tearoff=False)
        world_menu.add_command(label="Validate world", accelerator="F5", command=self.show_validation)
        world_menu.add_command(label="Build Unity preview", command=self.build_unity_preview)
        world_menu.add_separator()
        world_menu.add_command(label="New landmass", command=lambda: self.set_mode("add_landmass"))
        world_menu.add_command(label="Place city", command=lambda: self.set_mode("add_city"))
        world_menu.add_command(label="Draw road", command=lambda: self.set_mode("draw_road"))
        world_menu.add_command(label="Place POI", command=lambda: self.set_mode("add_poi"))
        world_menu.add_command(label="Place city gate", command=lambda: self.set_mode("add_gate"))
        world_menu.add_command(label="Place story spawn", command=lambda: self.set_mode("add_story_spawn"))
        menu.add_cascade(label="World", menu=world_menu)
        self.root.configure(menu=menu)

    def _make_toolbar(self) -> None:
        bar = self.ttk.Frame(self.root, padding=(8, 6))
        bar.pack(fill="x")
        for label, mode in (
            ("Select / move", "select"),
            ("New landmass", "add_landmass"),
            ("Place city", "add_city"),
            ("Draw road", "draw_road"),
            ("Place POI", "add_poi"),
            ("Place gate", "add_gate"),
            ("Place story spawn", "add_story_spawn"),
        ):
            self.ttk.Button(bar, text=label, command=lambda value=mode: self.set_mode(value)).pack(side="left", padx=2)
        self.finish_button = self.ttk.Button(bar, text="Finish road (Enter)", command=self.finish_road)
        self.finish_button.pack(side="left", padx=(10, 2))
        self.preview_button = self.ttk.Button(bar, text="Unity preview", command=self.build_unity_preview)
        self.preview_button.pack(side="right", padx=2)
        self.ttk.Button(bar, text="Validate", style="Accent.TButton", command=self.show_validation).pack(side="right", padx=2)
        self.ttk.Button(bar, text="Save", style="Accent.TButton", command=self.save).pack(side="right", padx=2)

    def _make_workspace(self) -> None:
        ttk = self.ttk
        pane = ttk.Panedwindow(self.root, orient="horizontal")
        pane.pack(fill="both", expand=True)

        left = ttk.Frame(pane, padding=(8, 0, 4, 4), width=350)
        right = ttk.Frame(pane, padding=(4, 0, 8, 4))
        pane.add(left, weight=0)
        pane.add(right, weight=1)

        tree_frame = ttk.LabelFrame(left, text="World items", padding=4)
        tree_frame.pack(fill="both", expand=False)
        self.tree = ttk.Treeview(tree_frame, show="tree", height=16, selectmode="browse")
        scroll = ttk.Scrollbar(tree_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scroll.set)
        self.tree.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")
        self.tree.bind("<<TreeviewSelect>>", self.on_tree_select)

        self.property_frame = ttk.LabelFrame(left, text="Properties", padding=8)
        self.property_frame.pack(fill="both", expand=True, pady=(8, 0))
        self.property_hint = ttk.Label(
            self.property_frame,
            text="Select an item on the map or in the list.",
            wraplength=300,
            foreground="#555555",
        )
        self.property_hint.grid(row=0, column=0, sticky="w")

        canvas_frame = ttk.LabelFrame(right, text="Top-down world (X / Z)", padding=2)
        canvas_frame.pack(fill="both", expand=True)
        self.canvas = self.tk.Canvas(canvas_frame, background=_rgb(OCEAN), highlightthickness=0, cursor="arrow")
        self.canvas.pack(fill="both", expand=True)
        self.canvas.bind("<Configure>", lambda _event: self.redraw())
        self.canvas.bind("<Button-1>", self.canvas_press)
        self.canvas.bind("<B1-Motion>", self.canvas_drag)
        self.canvas.bind("<ButtonRelease-1>", self.canvas_release)
        self.canvas.bind("<Button-3>", lambda _event: self.set_mode("select"))

        self.status = self.tk.StringVar(value="Ready")
        ttk.Label(right, textvariable=self.status, anchor="w").pack(fill="x", pady=(4, 0))

    def _bind_shortcuts(self) -> None:
        self.root.bind_all("<Control-o>", lambda _event: self.open_file())
        self.root.bind_all("<Control-s>", lambda _event: self.save())
        self.root.bind_all("<Control-S>", lambda _event: self.save_as())
        self.root.bind_all("<Control-z>", lambda _event: self.undo())
        self.root.bind_all("<Control-y>", lambda _event: self.redo())
        self.root.bind_all("<Delete>", lambda _event: self.delete_selected())
        self.root.bind_all("<F5>", lambda _event: self.show_validation())
        self.root.bind_all("<Return>", lambda _event: self.finish_road() if self.drawing_road is not None else None)
        self.root.bind_all("<Escape>", lambda _event: self.cancel_mode())

    # ----- data/list ------------------------------------------------------
    def refresh_all(self) -> None:
        self.rebuild_tree()
        self.show_properties()
        self.redraw()
        path = self.model.source_path.name if self.model.source_path else "Untitled"
        dirty = " *" if self.model.dirty else ""
        self.root.title(f"{APP_TITLE} — {path}{dirty}")
        self.finish_button.configure(state="normal" if self.drawing_road is not None else "disabled")

    def rebuild_tree(self) -> None:
        selected = f"{self.selection[0]}:{self.selection[1]}" if self.selection else ""
        self.tree.delete(*self.tree.get_children())
        roots = {
            "landmass": self.tree.insert("", "end", text="Landmasses", open=True),
            "road": self.tree.insert("", "end", text="Roads", open=True),
            "site": self.tree.insert("", "end", text="Runtime sites", open=True),
            "marker": self.tree.insert("", "end", text="Gates & story spawns", open=True),
        }
        for index, land in enumerate(self.model.data.get("Landmasses", [])):
            label = land.get("CityName") or land.get("Name") or f"Landmass {index + 1}"
            self.tree.insert(roots["landmass"], "end", iid=f"landmass:{index}", text=str(label))
        for index, _road in enumerate(self.model.data.get("Roads", [])):
            self.tree.insert(roots["road"], "end", iid=f"road:{index}", text=self.model.road_id(index))
        for index, site in enumerate(self.model.data.get("Sites", [])):
            label = site.get("DisplayName") or site.get("Id") or f"Site {index + 1}"
            self.tree.insert(roots["site"], "end", iid=f"site:{index}", text=f"{label} [{self.model.site_kind(site)}]")
        markers = ensure_editor_metadata(self.model.data).get("Markers", [])
        for index, marker in enumerate(markers):
            label = marker.get("DisplayName") or marker.get("Id") or f"Marker {index + 1}"
            self.tree.insert(roots["marker"], "end", iid=f"marker:{index}", text=f"{label} [{self.model.site_kind(marker, True)}]")
        if selected and self.tree.exists(selected):
            self.tree.selection_set(selected)
            self.tree.see(selected)

    def on_tree_select(self, _event: Any) -> None:
        if self.drawing_road is not None:
            drawing = f"road:{self.drawing_road}"
            if self.tree.exists(drawing):
                self.tree.selection_set(drawing)
            return
        selected = self.tree.selection()
        if not selected or ":" not in selected[0]:
            return
        kind, raw_index = selected[0].split(":", 1)
        if kind not in ("landmass", "road", "site", "marker"):
            return
        new_selection = kind, int(raw_index)
        if self.selection != new_selection and not self.commit_pending_properties():
            if self.selection:
                previous = f"{self.selection[0]}:{self.selection[1]}"
                if self.tree.exists(previous):
                    self.tree.selection_set(previous)
            return
        if self.selection != new_selection:
            self.road_point = None
        self.selection = new_selection
        self.show_properties()
        self.redraw()

    def selected_object(self) -> Optional[dict[str, Any]]:
        if not self.selection:
            return None
        kind, index = self.selection
        try:
            if kind == "landmass":
                return self.model.data["Landmasses"][index]
            if kind == "road":
                return self.model.data["Roads"][index]
            if kind == "site":
                return self.model.data["Sites"][index]
            if kind == "marker":
                return ensure_editor_metadata(self.model.data)["Markers"][index]
        except (KeyError, IndexError):
            self.selection = None
        return None

    # ----- property panels -----------------------------------------------
    def clear_properties(self) -> None:
        for child in self.property_frame.winfo_children():
            child.destroy()
        self.fields.clear()
        self.properties_pending = False

    def _mark_properties_pending(self, *_args: Any) -> None:
        self.properties_pending = True

    def add_field(self, row: int, key: str, label: str, value: Any, values: Optional[tuple[str, ...]] = None) -> None:
        self.ttk.Label(self.property_frame, text=label).grid(row=row, column=0, sticky="w", padx=(0, 8), pady=2)
        variable = self.tk.StringVar(value=str(value))
        variable.trace_add("write", self._mark_properties_pending)
        self.fields[key] = variable
        if values:
            widget = self.ttk.Combobox(self.property_frame, textvariable=variable, values=values, state="readonly", width=21)
        else:
            widget = self.ttk.Entry(self.property_frame, textvariable=variable, width=24)
        widget.grid(row=row, column=1, sticky="ew", pady=2)

    def show_properties(self) -> None:
        self.clear_properties()
        selected = self.selected_object()
        if selected is None or self.selection is None:
            self.ttk.Label(self.property_frame, text="Select an item on the map or in the list.", wraplength=300).grid(row=0, column=0, sticky="w")
            return
        kind, index = self.selection
        row = 0
        if kind == "landmass":
            center, size = selected.get("Center", {}), selected.get("Size", {})
            for key, label, value, choices in (
                ("Name", "Stable name", selected.get("Name", ""), None),
                ("Biome", "Biome", selected.get("Biome", "Halbrand"), tuple(BIOME_COLORS)),
                ("CityId", "City stable ID", selected.get("CityId", ""), None),
                ("CityName", "City display name", selected.get("CityName", ""), None),
                ("Center.x", "Centre X", center.get("x", 0), None),
                ("Center.y", "Base Y", center.get("y", 0), None),
                ("Center.z", "Centre Z", center.get("z", 0), None),
                ("Size.x", "Width", size.get("x", 0), None),
                ("Size.y", "Elevation / relief", size.get("y", 0), None),
                ("Size.z", "Depth", size.get("z", 0), None),
                ("PropCount", "Prop count", selected.get("PropCount", 0), None),
                ("TerrainSeed", "Terrain seed", selected.get("TerrainSeed", 0), None),
            ):
                self.add_field(row, key, label, value, choices)
                row += 1
            self.ttk.Label(
                self.property_frame,
                text="Drag to move. Shift-drag sets coastline width/depth.",
                wraplength=300,
                foreground="#555555",
            ).grid(row=row, column=0, columnspan=2, sticky="w", pady=(5, 2))
            row += 1
        elif kind == "road":
            self.add_field(row, "RoadId", "Stable road ID", self.model.road_id(index))
            row += 1
            points = selected.get("Points", [])
            self.ttk.Label(self.property_frame, text=f"Points: {len(points)}").grid(row=row, column=0, columnspan=2, sticky="w")
            row += 1
            point_index = self.road_point if self.road_point is not None and self.road_point < len(points) else (0 if points else None)
            self.road_point = point_index
            if point_index is not None:
                point = points[point_index]
                self.add_field(row, "PointIndex", "Selected point", point_index + 1)
                row += 1
                for axis in ("x", "y", "z"):
                    self.add_field(row, f"Point.{axis}", f"Point {axis.upper()}", point.get(axis, 0))
                    row += 1
            controls = self.ttk.Frame(self.property_frame)
            controls.grid(row=row, column=0, columnspan=2, sticky="ew", pady=4)
            self.ttk.Button(controls, text="Add point", command=self.add_road_point).pack(side="left", padx=(0, 3))
            self.ttk.Button(controls, text="Remove point", command=self.remove_road_point).pack(side="left")
            row += 1
            self.ttk.Label(
                self.property_frame,
                text="Click or drag a circular road handle on the map.",
                wraplength=300,
                foreground="#555555",
            ).grid(row=row, column=0, columnspan=2, sticky="w", pady=(2, 4))
            row += 1
        else:
            editor_only = kind == "marker"
            position = selected.get("WorldPosition", {})
            self.add_field(row, "Id", "Stable ID", selected.get("Id", "")); row += 1
            self.add_field(row, "DisplayName", "Display name", selected.get("DisplayName", "")); row += 1
            allowed = ("gate", "story_spawn") if editor_only else ("city", "poi")
            self.add_field(row, "Kind", "Kind", self.model.site_kind(selected, editor_only), allowed); row += 1
            for axis in ("x", "y", "z"):
                self.add_field(row, f"WorldPosition.{axis}", f"World {axis.upper()}", position.get(axis, 0)); row += 1
            if not editor_only:
                travel = selected.get("TravelPosition", {})
                for axis in ("x", "y", "z"):
                    self.add_field(row, f"TravelPosition.{axis}", f"Spawn {axis.upper()}", travel.get(axis, 0)); row += 1
                self.add_field(row, "DiscoverRadius", "Discover radius", selected.get("DiscoverRadius", 90)); row += 1
            else:
                self.ttk.Label(
                    self.property_frame,
                    text="Gate/story-spawn markers are editor-only; Unity ignores them until an importer chooses to consume them.",
                    wraplength=300,
                    foreground="#555555",
                ).grid(row=row, column=0, columnspan=2, sticky="w", pady=(5, 4))
                row += 1

        buttons = self.ttk.Frame(self.property_frame)
        buttons.grid(row=row, column=0, columnspan=2, sticky="ew", pady=(8, 0))
        self.ttk.Button(buttons, text="Apply", style="Accent.TButton", command=self.apply_properties).pack(side="left")
        self.ttk.Button(buttons, text="Delete", command=self.delete_selected).pack(side="right")
        self.property_frame.columnconfigure(1, weight=1)

    def field_float(self, key: str) -> float:
        raw = self.fields[key].get().strip()
        value = float(raw)
        if not math.isfinite(value):
            raise ValueError(f"{key} must be a finite number.")
        return value

    def field_int(self, key: str) -> int:
        raw = self.fields[key].get().strip()
        try:
            return int(raw)
        except ValueError as error:
            raise ValueError(f"{key} must be a whole number.") from error

    def apply_properties(self, refresh: bool = True) -> bool:
        from tkinter import messagebox

        if not self.selection or self.selected_object() is None:
            self.properties_pending = False
            return True
        if not self.properties_pending:
            return True
        kind, index = self.selection
        before = self.model.snapshot()
        try:
            selected = self.selected_object()
            assert selected is not None
            if kind == "landmass":
                selected["Name"] = self.fields["Name"].get().strip()
                selected["Biome"] = self.fields["Biome"].get()
                selected["CityId"] = self.fields["CityId"].get().strip()
                selected["CityName"] = self.fields["CityName"].get().strip()
                for group in ("Center", "Size"):
                    selected.setdefault(group, {})
                    for axis in ("x", "y", "z"):
                        selected[group][axis] = self.field_float(f"{group}.{axis}")
                selected["PropCount"] = self.field_int("PropCount")
                selected["TerrainSeed"] = self.field_int("TerrainSeed")
            elif kind == "road":
                meta = ensure_editor_metadata(self.model.data)
                meta["RoadIds"][index] = self.fields["RoadId"].get().strip()
                if self.road_point is not None and selected.get("Points"):
                    raw_point_index = self.field_int("PointIndex") - 1
                    if raw_point_index < 0 or raw_point_index >= len(selected["Points"]):
                        raise ValueError("Selected point is outside this road's point list.")
                    self.road_point = raw_point_index
                    point = selected["Points"][raw_point_index]
                    for axis in ("x", "y", "z"):
                        point[axis] = self.field_float(f"Point.{axis}")
            else:
                editor_only = kind == "marker"
                old_id = selected.get("Id")
                new_id = self.fields["Id"].get().strip()
                selected["Id"] = new_id
                selected["DisplayName"] = self.fields["DisplayName"].get().strip()
                chosen_kind = self.fields["Kind"].get()
                if editor_only:
                    selected["Kind"] = chosen_kind
                else:
                    meta = ensure_editor_metadata(self.model.data)
                    meta["SiteKinds"].pop(old_id, None)
                    meta["SiteKinds"][new_id] = chosen_kind
                    selected["IsCity"] = chosen_kind == "city"
                for axis in ("x", "y", "z"):
                    selected.setdefault("WorldPosition", {})[axis] = self.field_float(f"WorldPosition.{axis}")
                if not editor_only:
                    for axis in ("x", "y", "z"):
                        selected.setdefault("TravelPosition", {})[axis] = self.field_float(f"TravelPosition.{axis}")
                    selected["DiscoverRadius"] = self.field_float("DiscoverRadius")
            self.model.commit(f"Edit {kind}", before)
        except (ValueError, KeyError) as error:
            self.model.data = before
            messagebox.showerror("Could not apply properties", str(error), parent=self.root)
            return False
        self.properties_pending = False
        if refresh:
            self.refresh_all()
        return True

    def commit_pending_properties(self) -> bool:
        """Commit Entry/Combobox edits before any action can replace their widgets."""
        return not self.properties_pending or self.apply_properties(refresh=False)

    def add_road_point(self) -> None:
        if not self.commit_pending_properties():
            return
        if not self.selection or self.selection[0] != "road":
            return
        road = self.selected_object()
        if road is None:
            return
        points = road.setdefault("Points", [])
        if points:
            last = points[-1]
            new_point = {"x": float(last.get("x", 0)) + 100.0, "y": float(last.get("y", 0)), "z": float(last.get("z", 0))}
        else:
            new_point = {"x": 0.0, "y": 3.2, "z": 0.0}
        before = self.model.snapshot()
        points.append(new_point)
        self.road_point = len(points) - 1
        self.model.commit("Add road point", before)
        self.refresh_all()

    def remove_road_point(self) -> None:
        if not self.commit_pending_properties():
            return
        if not self.selection or self.selection[0] != "road" or self.road_point is None:
            return
        road = self.selected_object()
        if road is None:
            return
        points = road.get("Points", [])
        if not points or self.road_point >= len(points):
            return
        before = self.model.snapshot()
        del points[self.road_point]
        self.road_point = min(self.road_point, len(points) - 1) if points else None
        self.model.commit("Remove road point", before)
        self.refresh_all()

    # ----- canvas ---------------------------------------------------------
    def redraw(self) -> None:
        canvas = self.canvas
        canvas.delete("all")
        width, height = max(200, canvas.winfo_width()), max(200, canvas.winfo_height())
        self.transform = MapTransform(self.model.data, width, height, margin=34)
        transform = self.transform
        canvas.configure(background=_rgb(OCEAN))

        for x in range(math.ceil(transform.min_x / 1000) * 1000, math.floor(transform.max_x / 1000) * 1000 + 1, 1000):
            sx, _ = transform.world_to_screen(x, 0)
            canvas.create_line(sx, 0, sx, height, fill=_rgb(GRID))
            canvas.create_text(sx + 3, height - 12, text=f"{x}m", anchor="sw", fill="#6f8792", font=("Segoe UI", 8))
        for z in range(math.ceil(transform.min_z / 1000) * 1000, math.floor(transform.max_z / 1000) * 1000 + 1, 1000):
            _, sy = transform.world_to_screen(0, z)
            canvas.create_line(0, sy, width, sy, fill=_rgb(GRID))
            canvas.create_text(3, sy - 2, text=f"{z}m", anchor="sw", fill="#6f8792", font=("Segoe UI", 8))

        coast_factor = float(self.model.data.get("TerrainHalfExtent", 0.49))
        for index, land in enumerate(self.model.data.get("Landmasses", [])):
            center, size = land.get("Center", {}), land.get("Size", {})
            cx, cy = transform.world_to_screen(float(center.get("x", 0)), float(center.get("z", 0)))
            rx = float(size.get("x", 0)) * coast_factor * transform.scale
            ry = float(size.get("z", 0)) * coast_factor * transform.scale
            active = self.selection == ("landmass", index)
            fill = _rgb(BIOME_COLORS.get(str(land.get("Biome")), (115, 119, 105)))
            canvas.create_oval(cx - rx, cy - ry, cx + rx, cy + ry, fill=fill, outline="#fff2c7" if active else "#c1b78f", width=3 if active else 1, tags=(f"landmass:{index}",))
            label = land.get("CityName") or land.get("Name") or f"Landmass {index + 1}"
            canvas.create_text(cx, cy, text=f"{label}\n{float(size.get('y', 0)):g}m", justify="center", fill="#f7edcf", font=("Segoe UI", 9, "bold" if active else "normal"))

        for road_index, road in enumerate(self.model.data.get("Roads", [])):
            points = [transform.world_to_screen(float(p.get("x", 0)), float(p.get("z", 0))) for p in road.get("Points", [])]
            active = self.selection == ("road", road_index)
            if len(points) >= 2:
                canvas.create_line(*[coordinate for point in points for coordinate in point], fill="#fff0bd" if active else _rgb(ROAD), width=6 if active else 4, joinstyle="round", tags=(f"road:{road_index}",))
            if active or road_index == self.drawing_road:
                for point_index, (x, y) in enumerate(points):
                    selected_point = self.road_point == point_index
                    radius = 7 if selected_point else 5
                    canvas.create_oval(x - radius, y - radius, x + radius, y + radius, fill="#fff8dd" if selected_point else _rgb(ROAD), outline="#2a2318", width=2, tags=(f"roadpoint:{road_index}:{point_index}",))

        meta = ensure_editor_metadata(self.model.data)
        for kind, values in (("site", self.model.data.get("Sites", [])), ("marker", meta.get("Markers", []))):
            for index, site in enumerate(values):
                position = site.get("WorldPosition", {})
                x, y = transform.world_to_screen(float(position.get("x", 0)), float(position.get("z", 0)))
                active = self.selection == (kind, index)
                site_kind = self.model.site_kind(site, kind == "marker")
                color = MARKER if kind == "marker" else (CITY if site_kind == "city" else SITE)
                radius = 9 if active else 7
                if kind == "marker":
                    canvas.create_polygon(x, y - radius, x + radius, y, x, y + radius, x - radius, y, fill=_rgb(color), outline="#fff2c7" if active else "#2a2318", width=2, tags=(f"{kind}:{index}",))
                else:
                    canvas.create_oval(x - radius, y - radius, x + radius, y + radius, fill=_rgb(color), outline="#fff2c7" if active else "#2a2318", width=2, tags=(f"{kind}:{index}",))
                label = site.get("DisplayName") or site.get("Id") or kind
                canvas.create_text(x + 10, y - 9, text=f"{label} [{site_kind}]", anchor="sw", fill="#f5eacb", font=("Segoe UI", 8, "bold" if active else "normal"))

        canvas.create_rectangle(12, 12, 236, 74, fill="#172d3a", outline="#49606b")
        canvas.create_text(22, 21, text="● runtime site   ◆ editor marker", anchor="nw", fill="#f5eacb", font=("Segoe UI", 9))
        canvas.create_text(22, 43, text="Drag: move  ·  Shift-drag land: resize", anchor="nw", fill="#c8d1d1", font=("Segoe UI", 8))
        canvas.create_text(22, 58, text="Right-click / Esc: return to Select", anchor="nw", fill="#c8d1d1", font=("Segoe UI", 8))

    def hit_test(self, sx: float, sy: float) -> tuple[Optional[tuple[str, int]], Optional[int]]:
        if self.transform is None:
            return None, None
        transform = self.transform
        meta = ensure_editor_metadata(self.model.data)
        for kind, values in (("marker", meta.get("Markers", [])), ("site", self.model.data.get("Sites", []))):
            for index in range(len(values) - 1, -1, -1):
                point = values[index].get("WorldPosition", {})
                x, y = transform.world_to_screen(float(point.get("x", 0)), float(point.get("z", 0)))
                if math.hypot(sx - x, sy - y) <= 13:
                    return (kind, index), None
        for road_index, road in enumerate(self.model.data.get("Roads", [])):
            points = [transform.world_to_screen(float(p.get("x", 0)), float(p.get("z", 0))) for p in road.get("Points", [])]
            for point_index, (x, y) in enumerate(points):
                if math.hypot(sx - x, sy - y) <= 10:
                    return ("road", road_index), point_index
            if any(_distance_to_segment(sx, sy, *first, *second) <= 7 for first, second in zip(points, points[1:])):
                return ("road", road_index), None
        coast_factor = float(self.model.data.get("TerrainHalfExtent", 0.49))
        candidates = []
        wx, wz = transform.screen_to_world(sx, sy)
        for index, land in enumerate(self.model.data.get("Landmasses", [])):
            center, size = land.get("Center", {}), land.get("Size", {})
            rx, rz = float(size.get("x", 0)) * coast_factor, float(size.get("z", 0)) * coast_factor
            if rx <= 0 or rz <= 0:
                continue
            normalized = ((wx - float(center.get("x", 0))) / rx) ** 2 + ((wz - float(center.get("z", 0))) / rz) ** 2
            if normalized <= 1:
                candidates.append((rx * rz, index))
        if candidates:
            return ("landmass", min(candidates)[1]), None
        return None, None

    def surface_y(self, x: float, z: float) -> float:
        best = float(self.model.data.get("WaterLevel", 2.0)) + 1.0
        coast_factor = float(self.model.data.get("TerrainHalfExtent", 0.49))
        for land in self.model.data.get("Landmasses", []):
            center, size = land.get("Center", {}), land.get("Size", {})
            rx, rz = float(size.get("x", 0)) * coast_factor, float(size.get("z", 0)) * coast_factor
            if rx <= 0 or rz <= 0:
                continue
            if ((x - float(center.get("x", 0))) / rx) ** 2 + ((z - float(center.get("z", 0))) / rz) ** 2 <= 1:
                best = max(best, float(center.get("y", 0)) + float(size.get("y", 0)))
        return best

    def canvas_press(self, event: Any) -> None:
        if self.transform is None:
            return
        if not self.commit_pending_properties():
            return
        wx, wz = self.transform.screen_to_world(event.x, event.y)
        if self.mode == "draw_road":
            if self.drawing_road is None:
                self.drawing_before = self.model.snapshot()
                self.drawing_dirty_before = self.model.dirty
                self.model.data.setdefault("Roads", []).append({"Points": []})
                ensure_editor_metadata(self.model.data)
                self.model.dirty = True
                self.drawing_road = len(self.model.data["Roads"]) - 1
                self.selection = ("road", self.drawing_road)
            road = self.model.data["Roads"][self.drawing_road]
            road.setdefault("Points", []).append({"x": round(wx, 1), "y": round(self.surface_y(wx, wz), 1), "z": round(wz, 1)})
            self.road_point = len(road["Points"]) - 1
            self.status.set(f"Road point {self.road_point + 1} placed. Add another or press Enter.")
            self.refresh_all()
            return
        if self.mode.startswith("add_"):
            if self.mode == "add_landmass":
                index = self.model.add_landmass(round(wx, 1), round(wz, 1))
                self.selection = ("landmass", index)
            else:
                site_kind = {
                    "add_city": "city", "add_poi": "poi", "add_gate": "gate",
                    "add_story_spawn": "story_spawn"
                }[self.mode]
                self.selection = self.model.add_site(site_kind, round(wx, 1), round(self.surface_y(wx, wz), 1), round(wz, 1))
            self.set_mode("select")
            self.refresh_all()
            return

        selection, point = self.hit_test(event.x, event.y)
        self.selection, self.road_point = selection, point
        if selection:
            iid = f"{selection[0]}:{selection[1]}"
            if self.tree.exists(iid):
                self.tree.selection_set(iid)
                self.tree.see(iid)
            # Tree selection events can run while selection_set is being handled.
            # Restore the exact polyline handle chosen on the canvas afterwards.
            self.road_point = point
            selected = self.selected_object()
            if selected is not None:
                self.drag_before = self.model.snapshot()
                self.drag_dirty_before = self.model.dirty
                self.drag_anchor = (wx, wz)
                self.drag_original = self.model.snapshot()
                resize = selection[0] == "landmass" and bool(event.state & 0x0001)
                self.drag_label = "Resize coastline" if resize else "Move item"
        self.show_properties()
        self.redraw()

    def canvas_drag(self, event: Any) -> None:
        if not self.selection or self.transform is None or self.drag_anchor is None or self.drag_original is None:
            return
        wx, wz = self.transform.screen_to_world(event.x, event.y)
        anchor_x, anchor_z = self.drag_anchor
        dx, dz = wx - anchor_x, wz - anchor_z
        kind, index = self.selection
        try:
            if kind == "landmass":
                land = self.model.data["Landmasses"][index]
                original = self.drag_original["Landmasses"][index]
                if self.drag_label == "Resize coastline":
                    center = original["Center"]
                    factor = float(self.model.data.get("TerrainHalfExtent", 0.49))
                    land["Size"]["x"] = max(50.0, abs(wx - float(center.get("x", 0))) / factor)
                    land["Size"]["z"] = max(50.0, abs(wz - float(center.get("z", 0))) / factor)
                else:
                    land["Center"]["x"] = float(original["Center"].get("x", 0)) + dx
                    land["Center"]["z"] = float(original["Center"].get("z", 0)) + dz
            elif kind == "site":
                site = self.model.data["Sites"][index]
                original = self.drag_original["Sites"][index]
                for key in ("WorldPosition", "TravelPosition"):
                    site[key]["x"] = float(original[key].get("x", 0)) + dx
                    site[key]["z"] = float(original[key].get("z", 0)) + dz
            elif kind == "marker":
                site = ensure_editor_metadata(self.model.data)["Markers"][index]
                original = ensure_editor_metadata(self.drag_original)["Markers"][index]
                site["WorldPosition"]["x"] = float(original["WorldPosition"].get("x", 0)) + dx
                site["WorldPosition"]["z"] = float(original["WorldPosition"].get("z", 0)) + dz
            elif kind == "road" and self.road_point is not None:
                point = self.model.data["Roads"][index]["Points"][self.road_point]
                original = self.drag_original["Roads"][index]["Points"][self.road_point]
                point["x"] = float(original.get("x", 0)) + dx
                point["z"] = float(original.get("z", 0)) + dz
        except (KeyError, IndexError):
            return
        self.redraw()

    def canvas_release(self, _event: Any) -> None:
        if self.drag_before is not None:
            self.model.commit(self.drag_label, self.drag_before)
        self.drag_before = None
        self.drag_anchor = None
        self.drag_original = None
        self.refresh_all()

    # ----- commands -------------------------------------------------------
    def set_mode(self, mode: str) -> None:
        if not self.commit_pending_properties():
            return
        if self.drawing_road is not None and mode != "draw_road":
            self.finish_road()
            if self.drawing_road is not None:
                return
        self.mode = mode
        cursor = "crosshair" if mode != "select" else "arrow"
        self.canvas.configure(cursor=cursor)
        messages = {
            "select": "Select mode: click and drag items. Shift-drag a landmass to resize its coastline.",
            "add_landmass": "Click the map to place a new landmass.",
            "add_city": "Click dry land to place a runtime city site, then link its ID on the landmass.",
            "draw_road": "Click to place road points. Press Enter to finish, Esc to cancel.",
            "add_poi": "Click dry land to place a runtime point of interest.",
            "add_gate": "Click dry land to place an editor-only city gate marker.",
            "add_story_spawn": "Click dry land to place an editor-only story spawn marker.",
        }
        self.status.set(messages[mode])
        self.finish_button.configure(state="normal" if self.drawing_road is not None else "disabled")

    def finish_road(self) -> None:
        if not self.commit_pending_properties():
            return
        if self.drawing_road is None or self.drawing_before is None:
            return
        road = self.model.data["Roads"][self.drawing_road]
        count = len(road.get("Points", []))
        if count < 2:
            from tkinter import messagebox

            if not messagebox.askyesno("Road needs more points", "A road needs at least two points. Cancel this road?", parent=self.root):
                return
            self.cancel_road()
            return
        self.model.commit("Draw road", self.drawing_before)
        self.drawing_road = None
        self.drawing_before = None
        self.mode = "select"
        self.canvas.configure(cursor="arrow")
        self.status.set(f"Road saved with {count} points. Validate before saving the world file.")
        self.refresh_all()

    def cancel_road(self) -> None:
        self.properties_pending = False
        if self.drawing_before is not None:
            self.model.data = self.drawing_before
            self.model.dirty = self.drawing_dirty_before
        self.drawing_road = None
        self.drawing_before = None
        self.selection = None
        self.road_point = None
        self.mode = "select"
        self.canvas.configure(cursor="arrow")
        self.status.set("Road drawing cancelled.")
        self.refresh_all()

    def cancel_mode(self) -> None:
        if self.drawing_road is not None:
            self.cancel_road()
        else:
            if not self.commit_pending_properties():
                return
            self.set_mode("select")

    def undo(self) -> None:
        if not self.commit_pending_properties():
            return
        if self.drawing_road is not None:
            self.cancel_road()
            return
        if self.model.undo():
            self.selection = None
            self.road_point = None
            self.status.set("Undid " + self.model.redo_label + ".")
            self.refresh_all()

    def redo(self) -> None:
        if not self.commit_pending_properties():
            return
        if self.model.redo():
            self.selection = None
            self.road_point = None
            self.status.set("Redid " + self.model.undo_label + ".")
            self.refresh_all()

    def delete_selected(self) -> None:
        from tkinter import messagebox

        if not self.commit_pending_properties():
            return
        if not self.selection:
            return
        kind, index = self.selection
        if not messagebox.askyesno("Delete item?", f"Delete selected {kind}? Undo remains available.", parent=self.root):
            return
        if self.model.delete(kind, index):
            self.selection = None
            self.road_point = None
            self.status.set(f"Deleted {kind}. Validate before saving.")
            self.refresh_all()

    def show_validation(self) -> None:
        from tkinter import messagebox

        if not self.commit_pending_properties():
            return
        issues = validate_world(self.model.data)
        errors = sum(issue.severity == "error" for issue in issues)
        warnings = sum(issue.severity == "warning" for issue in issues)
        if not issues:
            messagebox.showinfo("World is valid", format_issues(issues), parent=self.root)
            self.status.set("Validation passed: stable IDs, dry spawns and road endpoints are valid.")
            return
        messagebox.showerror(
            f"Validation found {errors} error(s)",
            format_issues(issues[:20]) + (f"\n\n…and {len(issues) - 20} more." if len(issues) > 20 else ""),
            parent=self.root,
        )
        self.status.set(f"Validation: {errors} error(s), {warnings} warning(s).")

    def confirm_discard(self) -> bool:
        if not self.commit_pending_properties():
            return False
        if not self.model.dirty:
            return True
        from tkinter import messagebox

        answer = messagebox.askyesnocancel("Unsaved world", "Save your changes before continuing?", parent=self.root)
        if answer is None:
            return False
        if answer:
            return self.save()
        return True

    def open_file(self) -> None:
        if not self.confirm_discard():
            return
        from tkinter import filedialog, messagebox

        path = filedialog.askopenfilename(
            title="Open Unity world JSON",
            initialdir=str(self.model.source_path.parent if self.model.source_path else DEFAULT_WORLD.parent),
            filetypes=(("Unity world JSON", "*.world.json"), ("JSON", "*.json"), ("All files", "*.*")),
        )
        if not path:
            return
        try:
            self.model = WorldModel.load(path)
        except (OSError, ValueError) as error:
            messagebox.showerror("Could not open world", str(error), parent=self.root)
            return
        self.selection = None
        self.road_point = None
        self.status.set(f"Opened {path}")
        self.refresh_all()

    def save(self) -> bool:
        if not self.commit_pending_properties():
            return False
        if self.drawing_road is not None:
            self.finish_road()
            if self.drawing_road is not None:
                return False
        if self.model.source_path is None:
            return self.save_as()
        from tkinter import messagebox

        issues = validate_world(self.model.data)
        errors = [issue for issue in issues if issue.severity == "error"]
        if errors:
            messagebox.showerror("World is not safe to save", format_issues(errors[:20]), parent=self.root)
            self.status.set(f"Save blocked by {len(errors)} validation error(s).")
            return False
        try:
            backup = self.model.save()
        except (OSError, ValueError) as error:
            messagebox.showerror("Could not save world", str(error), parent=self.root)
            return False
        backup_text = f" Backup: {backup.name}" if backup else ""
        self.status.set(f"Saved {self.model.source_path.name}.{backup_text}")
        self.refresh_all()
        return True

    def save_as(self) -> bool:
        from tkinter import filedialog

        path = filedialog.asksaveasfilename(
            title="Save Unity world JSON",
            initialdir=str(self.model.source_path.parent if self.model.source_path else DEFAULT_WORLD.parent),
            initialfile=self.model.source_path.name if self.model.source_path else "ratna.world.json",
            defaultextension=".json",
            filetypes=(("Unity world JSON", "*.world.json"), ("JSON", "*.json")),
        )
        if not path:
            return False
        old_path = self.model.source_path
        self.model.source_path = Path(path).resolve()
        if not self.save():
            self.model.source_path = old_path
            return False
        return True

    def export(self, extension: str) -> None:
        from tkinter import filedialog, messagebox

        if not self.commit_pending_properties():
            return
        initial = (self.model.source_path.stem if self.model.source_path else "ratna-world") + "-preview" + extension
        path = filedialog.asksaveasfilename(
            title=f"Export {extension[1:].upper()} preview",
            initialdir=str(self.model.source_path.parent if self.model.source_path else Path.cwd()),
            initialfile=initial,
            defaultextension=extension,
            filetypes=((f"{extension[1:].upper()} preview", f"*{extension}"),),
        )
        if not path:
            return
        try:
            export_preview(self.model.data, path)
        except (OSError, ValueError) as error:
            messagebox.showerror("Could not export preview", str(error), parent=self.root)
            return
        self.status.set(f"Exported preview to {path}")

    def build_unity_preview(self) -> None:
        from tkinter import messagebox

        if self.unity_preview_process is not None:
            messagebox.showinfo("Unity preview", "A Unity preview is already running.", parent=self.root)
            return
        if self.model.source_path is None or self.model.source_path.resolve() != DEFAULT_WORLD.resolve():
            messagebox.showerror(
                "Unity preview uses the project world",
                "Save or open Assets/Resources/Data/World/kessil.world.json first. "
                "The shipping preview bridge deliberately refuses alternate files.",
                parent=self.root,
            )
            return
        if not self.save():
            return

        unity = find_unity_executable()
        if unity is None:
            messagebox.showerror(
                "Unity was not found",
                "Set RATNA_UNITY_PATH to Unity.exe, then try again.",
                parent=self.root,
            )
            return

        log_path = PROJECT_ROOT / "Temp" / "worldbuilder-preview.log"
        log_path.parent.mkdir(parents=True, exist_ok=True)
        creation_flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        try:
            self.unity_preview_process = subprocess.Popen(
                unity_preview_arguments(unity, log_path),
                cwd=PROJECT_ROOT,
                creationflags=creation_flags,
            )
            self.unity_preview_started_ns = time.time_ns()
        except OSError as error:
            self.unity_preview_process = None
            messagebox.showerror("Could not start Unity", str(error), parent=self.root)
            return

        self.preview_button.configure(state="disabled")
        self.status.set("Unity is validating, rebuilding and capturing the project world…")
        self.root.after(500, lambda: self._poll_unity_preview(log_path))

    def _poll_unity_preview(self, log_path: Path) -> None:
        from tkinter import messagebox

        process = self.unity_preview_process
        if process is None:
            return
        result = process.poll()
        if result is None:
            self.root.after(500, lambda: self._poll_unity_preview(log_path))
            return

        self.unity_preview_process = None
        self.preview_button.configure(state="normal")
        output = PROJECT_ROOT / "Docs" / "Screenshots" / "WorldBuilder"
        top_down = output / "world-top-down.png"
        perspective = output / "world-player-perspective.png"
        captures_are_fresh = all(
            path.is_file() and path.stat().st_mtime_ns >= self.unity_preview_started_ns
            for path in (top_down, perspective)
        )
        if result == 0 and captures_are_fresh:
            self.status.set("Unity preview passed; two shipping-world captures were updated.")
            messagebox.showinfo(
                "Unity preview complete",
                f"Validated and rebuilt the project world.\n\n{top_down}\n{perspective}",
                parent=self.root,
            )
            return

        tail = ""
        try:
            tail = "\n".join(log_path.read_text(encoding="utf-8", errors="replace").splitlines()[-20:])
        except OSError:
            pass
        self.status.set(f"Unity preview failed with exit code {result}.")
        messagebox.showerror(
            "Unity preview failed",
            f"Unity exited with code {result}. Review {log_path}.\n\n{tail}",
            parent=self.root,
        )

    def close(self) -> None:
        if self.confirm_discard():
            self.root.destroy()


def parse_arguments(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Visually edit and validate the Unity Version 1 Ratna Bay world document."
    )
    parser.add_argument("world", nargs="?", type=Path, default=DEFAULT_WORLD, help="world JSON (defaults to the project's kessil.world.json)")
    parser.add_argument("--validate", action="store_true", help="validate without opening a window")
    parser.add_argument("--preview", type=Path, metavar="PNG_OR_SVG", help="export a headless top-down .png or .svg preview")
    parser.add_argument("--width", type=int, default=1200, help="preview width (default: 1200)")
    parser.add_argument("--height", type=int, default=900, help="preview height (default: 900)")
    return parser.parse_args(argv)


def main(argv: Optional[list[str]] = None) -> int:
    arguments = parse_arguments(list(sys.argv[1:] if argv is None else argv))
    try:
        model = WorldModel.load(arguments.world)
    except (OSError, ValueError) as error:
        print(f"ERROR: Could not open '{arguments.world}': {error}", file=sys.stderr)
        return 2

    if arguments.validate or arguments.preview:
        issues = validate_world(model.data)
        print(format_issues(issues))
        errors = [issue for issue in issues if issue.severity == "error"]
        if arguments.preview:
            try:
                target = export_preview(model.data, arguments.preview, arguments.width, arguments.height)
                print(f"Preview written to {target.resolve()}")
            except (OSError, ValueError) as error:
                print(f"ERROR: Could not export preview: {error}", file=sys.stderr)
                return 2
        return 1 if errors else 0

    try:
        import tkinter as tk
    except ImportError:
        print("ERROR: This Python installation has no Tkinter. Run with a standard Python 3 for Windows install.", file=sys.stderr)
        return 2
    root = tk.Tk()
    WorldBuilderApp(root, arguments.world)
    root.mainloop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
