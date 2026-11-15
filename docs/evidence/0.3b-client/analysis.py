#!/usr/bin/env python3
"""Read a probe `panel` dump and report the layout facts the 0.3b gate cares about.

  usage: analysis.py <panel-log> [<panel-log> ...]

Prints, per file: screen size, container window rect, the eight category buttons' rects grouped
into the 4 x 2 grid they are drawn in, every overlap figure the probe computed, and the panel's
own bounds against the window. Exit code 1 if a category is missing, the grid is not 4 x 2, or the
panel overlaps the vanilla grid.
"""
import re
import sys

CATEGORIES = ["Metals", "Wood", "Food", "Trophies", "Valuables", "Building", "Consumables", "Tools"]
EL = re.compile(
    r"panel el name=(?P<name>\S+) active=(?P<active>\S+) vis=(?P<vis>\S+) clickable=(?P<click>\S+) "
    r"label='(?P<label>.*?)' color=#(?P<color>\S+) rect=(?P<x>-?[\d.]+),(?P<y>-?[\d.]+),(?P<w>[\d.]+)x(?P<h>[\d.]+)"
)
OVERLAP = re.compile(
    r"panel overlap (?P<name>\S+) vs grid = (?P<grid>[\d.]+) vs window = (?P<window>[\d.]+) "
    r"inside=(?P<inside>\S+) area=(?P<area>\d+)"
)
ROOT = re.compile(r"panel root=\S+ win=\S+ screen=(?P<screen>\S+) rect=(?P<rect>\S+)")


def parse(path):
    root = None
    elements = {}
    overlaps = {}
    pairs = {}
    for line in open(path, errors="replace"):
        m = ROOT.search(line)
        if m:
            root = m.groupdict()
        m = EL.search(line)
        if m:
            elements[m.group("name")] = m.groupdict()
        m = OVERLAP.search(line)
        if m:
            overlaps[m.group("name")] = m.groupdict()
        m = re.search(r"panel pairs (\S+) leaves=(\d+) overlapping=(\d+)", line)
        if m:
            pairs[m.group(1)] = (int(m.group(2)), int(m.group(3)))
    return root, elements, overlaps, pairs


def report(path):
    root, elements, overlaps, pairs = parse(path)
    print(f"== {path}")
    if not root:
        print("   no panel root line")
        return False
    print(f"   screen {root['screen']}  window rect {root['rect']}")

    ok = True
    cats = []
    for name in CATEGORIES:
        el = elements.get("HusnisseCat" + name)
        if not el or el["vis"] != "True":
            print(f"   MISSING category {name}")
            ok = False
            continue
        cats.append((name, float(el["x"]), float(el["y"]), el["w"], el["h"], el["color"]))

    if cats:
        xs = sorted({round(c[1], 1) for c in cats})
        ys = sorted({round(c[2], 1) for c in cats})
        print(f"   categories drawn: {len(cats)}  columns={len(xs)} rows={len(ys)}")
        for name, x, y, w, h, color in sorted(cats, key=lambda c: (-c[2], c[1])):
            print(f"     {name:<12} at ({x:7.1f},{y:7.1f}) {w}x{h}  color #{color}")
        if len(xs) != 4 or len(ys) != 2:
            print("   GRID IS NOT 4 x 2")
            ok = False

    for name, o in overlaps.items():
        verdict = "ok" if (name == "HusnisseNetworkStrip" or float(o["grid"]) == 0.0) else "OVERLAPS GRID"
        if name != "HusnisseNetworkStrip" and float(o["grid"]) > 0:
            ok = False
        print(f"   {name:<22} grid-overlap {o['grid']:>8}  window-overlap {o['window']:>8}  "
              f"inside={o['inside']}  area={o['area']}  {verdict}")

    for name, (leaves, bad) in pairs.items():
        print(f"   {name:<22} controls={leaves} overlapping-pairs={bad}")
        if bad:
            ok = False

    for key in ("HusnisseRulesPanel", "HusnisseCustomPanel"):
        el = elements.get(key)
        if el:
            print(f"   {key}: {'visible' if el['vis'] == 'True' else 'hidden'} "
                  f"{el['w']}x{el['h']} at ({el['x']},{el['y']})")
    return ok


def main():
    ok = True
    for path in sys.argv[1:]:
        ok = report(path) and ok
        print()
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
