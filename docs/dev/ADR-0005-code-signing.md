# ADR-0005: Code signing

- **Status:** Proposed — the tooling is built and proven; the certificate
  provider is a maintainer purchasing decision and is not yet made.
- **Date:** 2026-08-03
- **Deciders:** the Thrum maintainers

## Context

`0.9.0-beta.1` shipped unsigned. Windows shows "Windows protected your PC" on
first run, and there is no way for a user to confirm the download came from us
beyond the SHA-256 we publish alongside the release.

Two constraints shape every option.

**Private keys must live in hardware.** Since 2023 the CA/Browser Forum requires
code-signing keys to be held on a hardware token or in an HSM. Nobody issues a
plain key file any more. This is also why the signing key can never be held by
an automated agent working on this project: the whole point is that it stays
with the maintainer.

**Token-based certificates cannot sign in CI.** The token must be physically
present, so a workflow on a hosted runner cannot use one. This is the fact that
decides the architecture, and it is easy to discover only after paying:

| route | cost order | signs in CI? |
| --- | --- | --- |
| Certum open-source certificate (hardware card) | ~€70-100/yr | **No** — local only |
| SSL.com / Sectigo OV with cloud signing (eSigner) | ~$200-400/yr | Yes |
| Azure Trusted Signing | ~$10/mo | Yes, but the individual-developer tier is **US-only**; a Canadian maintainer needs a registered business entity |

The maintainer is an individual in Canada distributing a free GPL-3.0 program,
so the cheap token route is the likely choice and the CI-signing routes are
either geographically unavailable or disproportionately expensive.

## Decision

**Signing is a local release step, not a workflow job.** `release.yml` continues
to publish an unsigned archive; the maintainer signs on the machine holding the
token and attaches the signed archive.

`extras/sign-release.ps1` implements it: unpack, sign `Thrum.exe` and
`Thrum.dll` with `signtool` using a certificate identified by thumbprint,
RFC 3161 timestamp, verify, then repackage **beside** the original rather than
over it.

Design choices worth stating, because each is a refusal to do something easier:

- **Timestamping is mandatory, not a flag.** Without it every signature stops
  validating the day the certificate expires.
- **Only `Thrum.exe` and `Thrum.dll` are signed.** The .NET runtime files and
  satellite assemblies are Microsoft's and already signed; re-signing them
  would be wrong.
- **Verification reads the signature back from disk** rather than trusting
  signtool's exit code, so a missing timestamp or an untrusted chain is caught
  by us instead of by a user.
- **The original archive is never modified.** The published digest of the
  unsigned archive stays true, and the signed archive gets its own digest.
- **Every failure is fatal and explained.** No certificate, no signtool, a
  pattern matching nothing, or a failed verification all stop with the reason.
  Signing nothing and reporting success is the one outcome worth engineering
  against.

If a cloud-signing certificate is ever bought instead, this decision should be
revisited: the same verification logic applies, but the signing call moves into
`release.yml` with the credentials as repository secrets.

## Validation

The pipeline was proven end to end on 2026-08-03 with a **throwaway
self-signed certificate**, created in `CurrentUser\My` only and deleted
afterwards. It was deliberately **not** added to any trusted root store, so
machine trust was never modified.

Result: both binaries signed `sha256RSA`, both timestamped by DigiCert with the
timestamp chain verifying to a trusted root, and verification then failed with
exactly:

> A certificate chain processed, but terminated in a root certificate which is
> not trusted by the trust provider

which is the expected outcome for a self-signed certificate, and the script
correctly refused and left the archive untouched.

So the mechanism — signing, timestamping, verification, fail-closed refusal — is
proven. **What is not proven is the success path**, because that requires a
CA-issued certificate; the untrusted root is the only remaining fault, and a
real certificate resolves it by definition. That distinction should not be
blurred: this ADR claims a working pipeline, not a signed release.

## Consequences

- Releases stay unsigned until a certificate is bought. The published SHA-256
  remains the verification mechanism in the meantime, and the release notes
  explain the SmartScreen warning rather than leaving users to guess.
- SmartScreen reputation builds per-certificate over download volume, so early
  signed releases will still warn. Only Extended Validation certificates get
  immediate reputation, and they are out of scope on cost.
- The release procedure gains a manual step, which is a real cost: an unsigned
  release can be published by CI alone, a signed one cannot.
- The key never enters CI, this repository, or any automated agent's reach.
  That is a feature.
