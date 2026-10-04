#!/usr/bin/env python3
"""Set a cross-built Holdhint.exe to the Windows GUI subsystem and clear the checksum.

The .NET SDK marks the apphost as a console program when it is built on macOS.
A console subsystem flashes a terminal window for a tray app. Subsystem 2 is
the Windows GUI. The checksum is not required for a normal executable; zero it
after the header change.
"""

import struct
import sys
from pathlib import Path

MACHINES = {
    "x64": 0x8664,
    "arm64": 0xAA64,
}
NAMES = {value: name for name, value in MACHINES.items()}


def patch(path: Path, expected: str | None) -> None:
    data = bytearray(path.read_bytes())
    if data[:2] != b"MZ":
        raise SystemExit(f"{path} is not an executable")
    e_lfanew = struct.unpack_from("<I", data, 0x3C)[0]
    if data[e_lfanew:e_lfanew + 4] != b"PE\0\0":
        raise SystemExit(f"{path} has no PE signature")
    coff = e_lfanew + 4
    machine = struct.unpack_from("<H", data, coff)[0]
    optional = coff + 20
    magic = struct.unpack_from("<H", data, optional)[0]
    if magic != 0x20B:
        raise SystemExit(f"{path} is not PE32+ (magic {magic:#x})")
    checksum_off = optional + 64
    subsystem_off = optional + 68
    subsystem = struct.unpack_from("<H", data, subsystem_off)[0]
    print(f"{path.name}: machine {NAMES.get(machine, hex(machine))} subsystem {subsystem}")
    if expected is not None and machine != MACHINES[expected]:
        raise SystemExit(f"{path} is {NAMES.get(machine, hex(machine))}, expected {expected}")
    if subsystem != 2:
        struct.pack_into("<H", data, subsystem_off, 2)
        print("  subsystem set to 2 (Windows GUI)")
    struct.pack_into("<I", data, checksum_off, 0)
    path.write_bytes(data)
    check = bytearray(path.read_bytes())
    got = struct.unpack_from("<H", check, subsystem_off)[0]
    if got != 2:
        raise SystemExit(f"{path} subsystem is {got} after the patch")


if __name__ == "__main__":
    if len(sys.argv) not in (2, 3):
        raise SystemExit("usage: pack_pe.py Holdhint.exe [x64|arm64]")
    expected = sys.argv[2] if len(sys.argv) == 3 else None
    if expected is not None and expected not in MACHINES:
        raise SystemExit("arch must be x64 or arm64")
    patch(Path(sys.argv[1]), expected)
