#!/usr/bin/env python3
"""Compile-check Assets/Scripts without opening Unity.

Reuses the reference list + defines from Unity's generated Assembly-CSharp.csproj
and runs the Roslyn compiler that ships with the editor. Useful while the Unity
editor is open (batchmode can't take the project lock).

Usage:  python Tools/compile-check.py [--editor]
        --editor also checks Assembly-CSharp-Editor.csproj (Assets/Editor).
"""
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSC = (r"C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Data"
       r"\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll")


def parse(csproj):
    text = open(os.path.join(ROOT, csproj), encoding="utf-8-sig").read()
    sources = re.findall(r'<Compile Include="([^"]+)"', text)
    refs = re.findall(r"<HintPath>([^<]+)</HintPath>", text)
    defines = re.findall(r"<DefineConstants>([^<]*)</DefineConstants>", text)
    defines = defines[0].split(";") if defines else []
    return sources, refs, [d for d in defines if d.strip()]


def check(csproj, extra_refs=()):
    sources, refs, defines = parse(csproj)
    refs = list(refs) + list(extra_refs)
    out = os.path.join(tempfile.gettempdir(), csproj.replace(".csproj", "") + ".check.dll")

    args = ["-nologo", "-target:library", "-langversion:9.0", "-nostdlib+", "-noconfig",
            "-warn:0", "-out:" + out]
    args += ["-define:" + d.strip() for d in defines]
    args += ["-r:" + os.path.join(ROOT, r) for r in refs]
    args += [os.path.join(ROOT, s) for s in sources]

    rsp = os.path.join(tempfile.gettempdir(), csproj + ".rsp")
    with open(rsp, "w", encoding="utf-8") as fh:
        fh.write("\n".join('"%s"' % a if " " in a and not a.startswith("-") else a
                           for a in args))

    proc = subprocess.run(["dotnet", "exec", CSC, "@" + rsp],
                          capture_output=True, text=True, cwd=ROOT)
    errors = [l for l in (proc.stdout + proc.stderr).splitlines() if ": error " in l]
    print(f"--- {csproj}: {len(sources)} files, {len(refs)} refs -> "
          f"{'OK' if not errors else str(len(errors)) + ' ERROR(S)'}")
    for line in errors[:40]:
        print("   ", line.strip())
    return not errors


if __name__ == "__main__":
    ok = check("Assembly-CSharp.csproj")
    if "--editor" in sys.argv:
        # Editor assembly references the runtime one; point it at Unity's built copy.
        ok &= check("Assembly-CSharp-Editor.csproj")
    sys.exit(0 if ok else 1)
