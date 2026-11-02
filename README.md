# DaktiloClient

Portable Windows x64 remote-support **host** client. Source release1.2.0. The source in this repository builds DaktiloClient.exe directly with the .NET Framework compiler. No existing application EXE, decompiler, IL patcher or proprietary build tool is required to build it.

## Scope

Public repository: https://github.com/emreerosal/DaktiloClient . Maintainer/contact: https://github.com/emreerosal/DaktiloClient/issues .

Only the Client is released here. It displays a connection code, shares its local screen, accepts authorized remote input and services file/USB requests. There is no outbound viewer, receiver dashboard, administrator application binary or administrator UI source. Client-side wire formats and interoperability code are necessarily visible in the Client source; they must not be treated as secrets.

The Client is MIT licensed. Unmodified upstream usbipd-win binaries/source retain their GPL-3.0-only and upstream notices. Windows/.NET are system dependencies. See third-party/NOTICE.txt and docs/DEPENDENCIES.md. The included original logo has not been altered. Trademark rights and endorsement are not granted by the source license.

## Build

Windows x64 with .NET Framework4.8 runtime/compiler:

```powershell
./build.ps1
./scripts/test.ps1
```

Output: artifacts/DaktiloClient.exe, a single unsigned executable containing the logo, host MSI and its source/license notices. No network download is needed to build. Hardware-driver installation is not performed by the tests. USB self-test uses loopback sockets and a fake USB backend, and the UI test uses preview mode. Test success is not physical-device or long-duration WAN certification.

## Before running

Read [Privacy and access](docs/PRIVACY.md) and [System changes and uninstall](docs/UNINSTALL.md). Launching this app initiates USB-host preparation and relay registration. Missing USB drivers require Windows administrator approval. Give the displayed nine-digit code only to the support operator you intend to authorize. A code holder can access screen, files and shared USB devices and can use keyboard/mouse if enabled; there is no additional per-session approval prompt. Stop terminates the application's session; closing does not uninstall USB drivers or undo persistent USB bindings.

This Client routes through the configured TLS relay, not P2P. It pins the server certificate. The relay learns the pairing key and must be trusted. This project does not claim that the relay cannot access session contents. It is a visible foreground tool, not a hidden service or unattended startup agent.

## Code signing policy

See [Code signing policy](docs/CODE_SIGNING_POLICY.md). **Status: application preparation only; NOT accepted by SignPath Foundation and NOT signed.** The signing workflow is disabled unless an operator explicitly configures it after approval. No signing fees are charged by this source repository, but free signing eligibility is determined solely by the provider. SmartScreen warnings may still occur.

## Publishing

Publish only this directory as its own public repository. Do not upload a parent workspace or its private history. Add the actual maintainer/reviewer/approver identities, official homepage, service operator contact and public issue URL before applying. Use [application checklist](docs/SIGNPATH_APPLICATION.md). The other application and server deployment files are not part of this repository.

Source files reconstructed from the project's own Client binary are identified in docs/PROVENANCE.md. All build inputs are included; tests verify product metadata, absence of receiver UI classes and preservation of the original logo. This is a source-build release, not a claim of byte-identical reproduction of an earlier compiler output.



## Power behavior
While the normal Client window is open, a thread-scoped Windows execution request prevents automatic idle sleep and display timeout. It is released on close; power-plan settings are not changed. Manual sleep, lid actions, critical battery behavior and system policy can still suspend the computer. Preview/test modes do not hold the request. The power self-test acquires and releases it briefly.

