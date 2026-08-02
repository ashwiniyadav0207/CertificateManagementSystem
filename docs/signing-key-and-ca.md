# Signing keys and the certificate authority

Every certificate PDF is cryptographically signed so tampering can be detected. Rather than paying a commercial CA, the platform includes an offline tool that lets your organization become its own certificate authority (CA) — free, and entirely under your control, at the cost of not being in anyone's default trust store (see [Why Adobe won't show its own checkmark](#why-adobe-wont-show-its-own-checkmark) below).

## Running the CA tool

The CA tool lives at `tools/CertificateEngine.CaTool` and is run once to bootstrap the CA, then again only when you need to rotate the signing certificate:

```
dotnet run --project tools/CertificateEngine.CaTool -- init --organization "My NGO" --out data/ca --root-years 20 --signing-years 2
```

- `--organization` — your organization's name, embedded in the certificate subject.
- `--out` — output directory for the generated files.
- `--root-years` — validity period for the root CA certificate (make this long — it's expensive to change later, since every signing certificate and every certificate you've ever issued chains up to it).
- `--signing-years` — validity period for the day-to-day signing certificate (shorter — plan to rotate this periodically).

### Passwords

The two private keys generated (root and signing) are password-protected. Supply passwords via:

- `--root-password` / `--signing-password` flags, or
- `CA_ROOT_PASSWORD` / `CA_SIGNING_PASSWORD` environment variables, or
- neither — the tool will auto-generate strong passwords and **print them once**.

If you let the tool auto-generate passwords, capture them immediately (copy into a password manager) — they are not stored anywhere and won't be shown again.

## The three output files

| File | Contents | Sensitivity | Where it lives |
| --- | --- | --- | --- |
| `root-ca.pem` | Root CA's **public** certificate only | Safe to publish/distribute | Can be published on your website, handed to auditors, or imported into Adobe Acrobat's trusted certificate list by anyone who wants to see a "trusted" checkmark. |
| `root-ca.pfx` | Root certificate **+ private key** | Extremely sensitive | Only needed when issuing or rotating a signing certificate. Move to offline/cold storage (e.g. an encrypted USB drive in a safe, or an air-gapped machine) immediately after use — **do not leave it on the server.** |
| `signing.pfx` | Signing certificate **+ private key + chain to the root** | Extremely sensitive | This is what the running app loads, via `Signing:PfxPath` / `Signing:PfxPassword`. Lives on the production server, and only there. |

None of these should ever be committed to git — `*.pfx` and `*.pem` are already covered by this repo's `.gitignore`.

## Protecting `root-ca.pfx`

- Copy it off the VM the moment `init` finishes running (and ideally run `init` itself somewhere other than the production server).
- Store it encrypted, offline, and away from day-to-day systems — it should only need to come out of storage when you rotate `signing.pfx` (roughly every `--signing-years`, or immediately if `signing.pfx` is ever suspected of being compromised).
- Anyone who can read `root-ca.pfx` can mint a signing certificate that this platform's verification page will treat as fully trusted — treat access to it like access to your organization's bank account.

## Protecting `signing.pfx` on the server

- Restrict filesystem permissions to the service account only (see [`deploy/systemd/certificate-engine.service`](../deploy/systemd/certificate-engine.service), which runs the app as a dedicated, unprivileged `certeng` user with no login shell).
- Set `Signing:PfxPassword` via environment variable (the systemd unit's `EnvironmentFile`), never in `appsettings.json`.
- Back it up (encrypted) alongside the SQLite database — see [`docs/hosting.md`](hosting.md) — since losing it means losing the ability to re-verify the signature chain for certificates already issued against it, not just future issuance. (Timestamping — see below — mitigates this somewhat for already-issued PDFs.)
- Set `Signing:TimestampServerUrl` to an RFC 3161 timestamp authority (a commercial CA's TSA, or a free public one, e.g. FreeTSA.org — verify current availability/terms before depending on it) if you want already-issued certificates to remain provably signed "at the time" even after `signing.pfx` itself later expires or is rotated.

## Optional hardening ideas for later

These aren't required to get started, but are worth considering as the platform matures:

- **TPM sealing.** If the host offers a TPM (physical or virtual — some cloud VM SKUs expose a vTPM), use `tpm2-tools` to seal the PFX passphrase (or the PFX itself) so it can only be unsealed on that specific machine/boot state, rather than being readable as a plain file even by someone with filesystem access.
- **OS-level encryption at rest.** Where no TPM is available, put `signing.pfx` on a LUKS-encrypted volume, or use a dedicated secrets manager/vault product instead of a plain file.
- **Systemd sandboxing.** Already applied in [`deploy/systemd/certificate-engine.service`](../deploy/systemd/certificate-engine.service) — a dedicated unprivileged user, `NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`, a minimal `ReadWritePaths`, and an empty `CapabilityBoundingSet`. Review and extend this further as your threat model demands.
- **Shamir's Secret Sharing for the passphrase.** Split the `signing.pfx` (or, more usefully, the `root-ca.pfx`) passphrase across several trusted people (e.g. board members or senior staff) using a Shamir's Secret Sharing tool (e.g. `ssss`), such that any *k*-of-*n* of them must cooperate to reconstruct it. That way, no single compromised laptop or single coerced/careless individual can unlock the root key alone.

## Why Adobe won't show its own checkmark

Because the root CA is self-issued, it isn't in Adobe's (or anyone else's) default list of trusted root certificates. Opening a certificate PDF in Adobe Reader/Acrobat will typically show the signature as **present and unmodified, but from an untrusted issuer**, unless a specific user has manually imported `root-ca.pem` into their own trusted certificate list. **This is expected, and not a bug.**

The actual source of truth is this platform's own public verification page (`/verify/{publicId}`), not Adobe's UI. When someone verifies a certificate there, the server independently:

1. Recomputes the SHA-256 hash of the certificate file (`fileSha256` in the API response) to confirm the file hasn't been altered.
2. Re-validates the embedded signature against the organization's own root (`Signing:TrustedRootPath`) on the server side, and reports `signatureValid` and `signerSubject`.

So a recipient (or anyone checking on their behalf, e.g. an employer) should be pointed at the verification page/QR code as the authoritative check, not at whatever trust indicator their local PDF viewer happens to show. It's worth mentioning this explicitly wherever certificates are handed out, so recipients aren't confused by Adobe's "untrusted signer" banner.
