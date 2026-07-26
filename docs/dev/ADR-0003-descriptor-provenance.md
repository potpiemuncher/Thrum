# ADR-0003: Descriptor provenance and emulated device identity

- **Status:** Accepted
- **Date:** 2026-07-25
- **Deciders:** the Thrum maintainers

> Numbering note: an installer-technology ADR was sketched in planning as
> "ADR-0003". That number is taken by this record; the installer decision gets
> the next free number when it is written.

## Context

Thrum presents virtual controllers to games through the VIIPER backend. Those
virtual controllers are not generic HID devices — they advertise the vendor
and product IDs, USB descriptors, HID report descriptors, and string
descriptors of real Sony and Nintendo hardware: DualShock 4, DualSense,
DualSense Edge, and Switch 2 Pro (`057E:2069`), alongside an Xbox 360 persona.

This is unavoidable rather than decorative. Games do not detect controllers by
capability; they detect them by **exact identity**. A title decides whether to
show PlayStation glyphs, whether to expose adaptive triggers, whether to
enable gyro, and in many cases whether to accept the device at all, by
matching VID/PID against a hardcoded table and by parsing a report descriptor
it expects byte-for-byte. A virtual DualSense that advertises anything other
than a DualSense identity is, for practical purposes, not a DualSense: the
feature the user asked for does not work.

Producing those identities correctly requires byte-exact descriptor assets,
which are obtained by capturing them from physical hardware. Thrum inherits
such assets in two places — the descriptor data inside the VIIPER backend it
drives, and captured descriptor and report fixtures in the maintainers' older
work, previously justified by a provenance dossier
(`native_mode_descriptor_provenance.md`).

The open question was whether a new product should ship this at all, and if
so, under what discipline.

## Decision

**Adopted: Thrum ships emulated Sony and Nintendo device identities the way
the surrounding ecosystem already does.**

Specifically:

1. **Emulated identities ship.** Virtual controllers advertise the vendor,
   product, descriptor, and string identity of the real device they emulate,
   as VIIPER already does upstream and as ViGEm-based and DSX-style tools in
   this ecosystem have long done. Thrum does not invent a "generic" persona
   as a default; a persona that games do not recognise is not a feature.

2. **Every captured descriptor asset carries a provenance dossier.** For each
   asset or asset family committed to this repository there is a dossier
   recording: what device class the capture came from, firmware or revision
   where known, capture method and tooling, capture date, what was captured
   (device descriptor, configuration descriptor, HID report descriptor, string
   descriptors, report samples), what was modified or synthesised afterwards
   and why, and who to ask about it (the Thrum maintainers, not an individual).

3. **Every captured asset carries a SHA-256 hash inventory.** A machine-
   checkable inventory lists each committed asset file with its SHA-256. Tests
   that depend on a fixture assert against the inventory, so a silent edit to
   a byte-exact asset fails CI instead of quietly changing what the product
   emulates.

4. **No serial numbers, no personal data, ever.** Captures are redacted before
   commit. Prohibited in committed assets and dossiers: device serial numbers
   and per-unit identifiers, Bluetooth device addresses and pairing keys,
   MAC addresses, machine names, user account names, local filesystem paths,
   personal names, and email addresses. Where a descriptor field carries a
   per-unit value, it is replaced with a documented, obviously-synthetic
   placeholder and the substitution is recorded in the dossier. If a capture
   cannot be redacted without destroying the property under test, it stays out
   of the repository and the test is restructured.

5. **Dossiers are public.** The redacted dossier and inventory live in the
   repository next to the assets. Provenance that only the maintainers can see
   is not provenance.

6. **Scope.** This policy governs descriptor and protocol assets used for
   emulation and testing. It does not authorise redistributing vendor
   firmware, vendor software, or copyrighted vendor artwork; those remain out
   of scope and are handled separately under `NOTICE.txt`.

## Rationale

- **Compatibility is the whole point.** Identity-based detection is how the
  games actually behave. Shipping an unrecognisable persona would mean
  shipping a product whose central feature silently does not work.
- **It matches established ecosystem practice.** ViGEm, VIIPER, and DSX-style
  tools all present real controller identities to Windows and to games; this
  is the norm the platform's controller-emulation layer has operated under for
  years. Thrum is adopting an existing practice, not opening a new front.
- **Thrum's own upstream already does it.** VIIPER, the backend Thrum drives,
  contains these identities. Refusing them in the application while depending
  on a backend that provides them would be a distinction without a
  difference — and would only remove the discipline, not the behaviour.
- **The discipline is where the value is.** What distinguishes a defensible
  position from an undocumented one is not whether descriptors are used, but
  whether their origin is written down, whether their bytes are pinned, and
  whether personal data was stripped. Dossiers plus hash inventories make
  each asset auditable by anyone reading the repository.
- **Hash inventories protect correctness, not just provenance.** Byte-exact
  assets are exactly the kind of file that gets "tidied" — reformatted,
  re-indented, line-ending-converted — with no visible symptom until a game
  stops recognising the device. Pinning hashes turns that into a test failure.
- **Redaction is a hard floor.** A serial number in a fixture is a permanent,
  public, personal identifier attached to a specific piece of hardware. There
  is no benefit that justifies it, and no capture that requires it.

## Consequences

- Any pull request adding or changing a captured descriptor asset must add or
  update its dossier and its hash inventory entry in the same change. A change
  without both is incomplete.
- The inherited provenance dossier from the maintainers' earlier work is
  brought into this repository, re-reviewed against the redaction rules above,
  and re-published in redacted form when the corresponding fixtures are
  imported for use as a test corpus.
- Existing inherited assets are audited against this policy before the first
  release, not after.
- Emulated-identity behaviour stays documented in user-facing terms: the
  support matrix records which persona each virtual output presents, so the
  behaviour is visible rather than implicit.
