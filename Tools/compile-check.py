#!/usr/bin/env python3
"""Compile-check the project's C# without opening Unity.

Borrows the reference list and preprocessor defines from Unity's generated
Assembly-CSharp.csproj, but discovers source files from disk — the csproj is only
regenerated when Unity refreshes, so newly added scripts would otherwise be
skipped. Runs the Roslyn compiler that ships with the editor, which means this
works while the Unity editor is open and holding the project lock.

Usage:  python Tools/compile-check.py            # runtime scripts
        python Tools/compile-check.py --editor   # + Assets/Editor
"""
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSC = (r"C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Data"
       r"\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll")

RUNTIME_ROOTS = ["Assets/Scripts"]
EDITOR_ROOTS = ["Assets/Editor"]


def csproj_settings(csproj):
    text = open(os.path.join(ROOT, csproj), encoding="utf-8-sig").read()
    refs = re.findall(r"<HintPath>([^<]+)</HintPath>", text)
    defines = re.findall(r"<DefineConstants>([^<]*)</DefineConstants>", text)
    defines = defines[0].split(";") if defines else []
    return refs, [d.strip() for d in defines if d.strip()]


def sources(roots):
    found = []
    for root in roots:
        for dirpath, _, files in os.walk(os.path.join(ROOT, root)):
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
    status = "OK" if ok else "%d ERROR(S)" % max(len(errors), 1)
    print("--- %-8s %3d files -> %s" % (label, len(srcs), status))
    for line in errors[:40]:
        print("   ", line.strip().replace(ROOT + os.sep, ""))
    if not errors and proc.returncode != 0:
        print("    compiler exited", proc.returncode, (proc.stdout + proc.stderr)[:400])
    return ok


if __name__ == "__main__":
    refs, defines = csproj_settings("Assembly-CSharp.csproj")
    runtime = sources(RUNTIME_ROOTS)
    ok = compile_check("runtime", runtime, refs, defines)

    if "--editor" in sys.argv:
        # Compiled together with the runtime scripts it depends on.
        ed_refs, ed_defines = csproj_settings("Assembly-CSharp-Editor.csproj")
        ok &= compile_check("editor", runtime + sources(EDITOR_ROOTS), ed_refs, ed_defines)

    sys.exit(0 if ok else 1)
