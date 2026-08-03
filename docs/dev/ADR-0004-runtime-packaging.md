# ADR-0004: Runtime packaging

- **Status:** Accepted
- **Date:** 2026-08-03
- **Deciders:** the Thrum maintainers

## Context

Thrum targets .NET 8 WPF. Its release and CI workflows previously published
framework-dependent artifacts, which require the .NET 8 Desktop Runtime to be
installed on the destination machine. On a clean machine without that runtime,
`Thrum.exe` displayed the runtime-install prompt and exited.

That packaging configuration did not match the builds used for validation. The
decisive finding is: the self-contained configuration is the only one any
validation has ever exercised, including on a guest with no runtime installed;
the framework-dependent artifact has never been run successfully on a clean
machine.

## Decision

Release and CI artifacts are published self-contained for the `win-x64` runtime
identifier. The workflows retain the x64 MSBuild `Platform` property because it
selects the repository's canonical solution configuration independently of the
runtime identifier.

The packaging pipeline continues to post-process the publish directory, move
satellite assemblies into `Lang/`, inject the entry-assembly path into
`Thrum.deps.json`, record all package-owned files in
`.thrum-managed-files.txt`, and produce the existing `Thrum` directory and zip
layout.

This decision does not enable `PublishSingleFile`, `PublishTrimmed`, or
`PublishReadyToRun`. ARM64 artifacts and an installer remain out of scope.

## Consequences

- A win-x64 release or CI artifact starts without a separately installed .NET 8
  Desktop Runtime.
- The unpacked artifact grows from approximately 40 MB to approximately 196 MB;
  the zip grows to approximately 80–90 MB.
- .NET runtime security fixes require a new Thrum release because the runtime is
  shipped inside each artifact rather than supplied by a shared installation.
- The managed-files manifest contains hundreds of runtime files. They are
  package-owned files and must remain in the manifest so a future updater can
  remove runtime files that no longer ship without touching user-created data.
- The release surface remains deliberately narrow: win-x64 only, with no
  single-file, trimming, ReadyToRun, ARM64, or installer commitment.
