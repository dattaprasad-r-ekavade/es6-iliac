#!/usr/bin/env python3
"""Compile-check the project's C# without opening Unity.

For each assembly definition, this borrows the reference list and preprocessor
defines from the csproj Unity generates for it, but discovers source files from
disk — the csprojs are only regenerated on refresh, so newly added scripts would
otherwise be skipped. It runs the Roslyn compiler shipped with the editor, so it
works while the Unity editor is open and holding the project lock.

Because each assembly is compiled against its *own* reference set, this also
catches a missing asmdef reference rather than silently succeeding.

Usage:  python Tools/compile-check.py
"""
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSC = (r"C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Data"
       r"\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll")

# Assemblies are discovered from the csprojs Unity generates next to the project.
# Fall back to Assembly-CSharp when no asmdefs exist yet.
FALLBACK = [("Assembly-CSharp", "Assets")]


def discover():
    """[(csproj_name, source_root)] for every asmdef-backed assembly."""
    found = []
    for entry in sorted(os.listdir(ROOT)):
        if not entry.endswith(".csproj"):
            continue
        name = entry[:-len(".csproj")]
        if name.startswith("Assembly-CSharp"):
            continue
        text = open(os.path.join(ROOT, entry), encoding="utf-8-sig").read()
        m = re.search(r'<None Include="([^"]*\.asmdef)"', text)
        if not m:
            continue
        root = os.path.dirname(m.group(1).replace("\\", "/"))
        found.append((name, root))
    return found or FALLBACK


def settings(csproj):
    text = open(os.path.join(ROOT, csproj + ".csproj"), encoding="utf-8-sig").read()
    refs = re.findall(r"<HintPath>([^<]+)</HintPath>", text)

    # Dependencies on other asmdefs come through as ProjectReference, not HintPath.
    # Point those at the assemblies Unity has already built.
    for dep in re.findall(r'<ProjectReference Include="([^"]+)\.csproj"', text):
        dll = os.path.join("Library", "ScriptAssemblies", os.path.basename(dep) + ".dll")
        if os.path.exists(os.path.join(ROOT, dll)):
            refs.append(dll)

    defines = re.findall(r"<DefineConstants>([^<]*)</DefineConstants>", text)
    defines = defines[0].split(";") if defines else []
    return refs, [d.strip() for d in defines if d.strip()]


def sources(root):
    found = []
    for dirpath, _, files in os.walk(os.path.join(ROOT, root)):
        # Nested asmdefs form their own assembly; don't pull them in twice.
        if dirpath != os.path.join(ROOT, root) and \
           any(f.endswith(".asmdef") for f in os.listdir(dirpath)):
            continue
        found += [os.path.join(dirpath, f) for f in files if f.endswith(".cs")]
    return sorted(found)


def quote(arg):
    """Response-file quoting. Paths contain spaces ("Program Files", the project
    folder itself), and for -flag:value args only the value may be quoted."""
    if arg.startswith("-"):
        flag, sep, value = arg.partition(":")
        if sep and " " in value:
            return '%s:"%s"' % (flag, value)
        return arg
    return '"%s"' % arg if " " in arg else arg


def compile_check(label, srcs, refs, defines):
    if not srcs:
        print("--- %-14s (no sources)" % label)
        return True

    out = os.path.join(tempfile.gettempdir(), label + ".check.dll")
    # -noconfig must not appear inside a response file (csc warns and ignores it).
    args = ["-nologo", "-target:library", "-langversion:9.0", "-nostdlib+",
            "-warn:0", "-out:" + out]
    args += ["-define:" + d for d in defines]
    args += ["-r:" + (r if os.path.isabs(r) else os.path.join(ROOT, r)) for r in refs]
    args += srcs

    rsp = os.path.join(tempfile.gettempdir(), label + ".rsp")
    with open(rsp, "w", encoding="utf-8") as fh:
        fh.write("\n".join(quote(a) for a in args))

    proc = subprocess.run(["dotnet", "exec", CSC, "-noconfig", "@" + rsp],
                          capture_output=True, text=True, cwd=ROOT)
    errors = [l for l in (proc.stdout + proc.stderr).splitlines() if "error CS" in l]
    ok = proc.returncode == 0 and not errors
    print("--- %-14s %3d files -> %s" % (label, len(srcs), "OK" if ok else
                                         "%d ERROR(S)" % max(len(errors), 1)))
    for line in errors[:40]:
        print("   ", line.strip().replace(ROOT + os.sep, ""))
    if not errors and proc.returncode != 0:
        print("    compiler exited", proc.returncode, (proc.stdout + proc.stderr)[:400])
    return ok


if __name__ == "__main__":
    ok = True
    for name, root in discover():
        refs, defines = settings(name)
        ok &= compile_check(name, sources(root), refs, defines)
    sys.exit(0 if ok else 1)
