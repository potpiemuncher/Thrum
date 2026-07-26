# Security Policy

Thrum drives virtual USB devices through a kernel driver. That makes two
things true at once: security reports here can involve kernel state, and the
artifacts most useful for diagnosing a crash are also the most dangerous to
publish. This policy exists to keep both facts manageable.

## Never attach a crash dump to a public issue

Windows kernel dumps (`MEMORY.DMP`, kernel and complete dumps, and most
minidumps produced by a bugcheck) contain **raw kernel memory**. In practice
that can include filesystem cache contents, in-flight network buffers,
credential material held by lsass or a driver, device serial numbers, machine
identifiers, and fragments of whatever else the machine was doing at the
moment of the fault. Attaching one to a GitHub issue publishes all of it,
permanently, to anyone.

Do not attach dumps to issues, pull requests, discussions, or gists. Do not
paste raw dump bytes. If you already have, ask for the issue to be deleted
rather than edited — edit history is public.

## Redacted source-level analysis is the sharing norm

The way to share a crash publicly is to share **conclusions, not memory**:

- Bugcheck code and parameters.
- The faulting module and, where known, the function and offset.
- The call stack, symbolised, with no raw data arguments.
- The reproduction sequence, in words.
- Driver package identities and versions involved.

That is enough to act on. Everything else — dump files, full register dumps,
unfiltered ETW or IFR traces, log files containing user paths — stays private.
Thrum's own diagnostic report formatter redacts user paths for this reason;
follow the same standard by hand when you write a report.

## Reporting a vulnerability

Report privately, through GitHub's **private vulnerability reporting** on this
repository (Security tab -> Report a vulnerability), which opens a private
security advisory visible only to you and the Thrum maintainers. Use it for
anything that could be exploited, and for any crash you believe is
security-relevant rather than merely a bug.

Please include the redacted analysis described above. If a dump or trace is
genuinely necessary, say so in the advisory and the maintainers will arrange a
private channel — do not attach it unprompted, and do not send it anywhere
public in the meantime.

Expect an acknowledgement within a few days. Thrum is a small project; there
is no paid bounty. Fix timelines depend on whether the defect is in Thrum, in
the VIIPER backend, or in the kernel driver below both.

## Known kernel-driver risk: usbip-win2

Thrum's virtual controllers reach Windows through the VIIPER backend, which
depends on the third-party kernel driver **usbip-win2**. The Thrum maintainers
classify every currently published usbip-win2 release as **experimental, not
production-approved**, because a request-lifetime race has been reproduced on
real hardware and confirmed at source level: teardown of a virtual USB
**audio** endpoint can overlap in-flight isochronous completions, corrupting
kernel heap and producing bugchecks `0xA` or `0x139`. Two contributing causes
were identified in the driver source — inline URB completion at raised IRQL
from receive, purge, and cancel stacks, where the UDE contract requires a DPC,
and a partial MDL chained into a socket send without ownership of the parent
pages. The issue is filed upstream as usbip-win2 issue #181. Controller-only
emulation, with no audio, microphone, or advanced-haptics endpoints, does not
exercise the affected path. Thrum ships a read-only, fail-closed driver
diagnostic (see the README) and treats any package its manifest does not list
as unvalidated. Report driver defects to the usbip-win2 project; report
Thrum's handling of them here.
