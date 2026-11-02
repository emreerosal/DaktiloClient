# Code signing policy

Status: pending application; no SignPath affiliation or certificate is claimed.

## Signing scope

Only DaktiloClient.exe built from this repository may be submitted. No remote administrator binary, USB installer, unrelated program or locally supplied prebuilt application may be signed using this project's certificate. The unchanged upstream USB host MSI remains embedded with its upstream signature and notices; we do not re-sign it.

## Roles

GitHub maintainer, committer and reviewer: [emreerosal](https://github.com/emreerosal). Intended release approver: emreerosal, subject to actual SignPath account enrollment and approval. Signing MUST remain disabled until the SignPath identities and roles are configured. All role holders must use MFA. External changes require review. Every production signing request requires an authorized human approval at SignPath. Repository administrators should protect the main branch, tags, signing secret and workflow changes.

## Build origin and artifact restrictions

The GitHub-hosted Windows workflow builds from checked-out source, runs the Client tests and uploads only artifacts/DaktiloClient.exe. SignPath must be configured to trust this repository/workflow and require ProductName DaktiloClient, FileDescription DaktiloClient and matching ProductVersion/FileVersion1.2.0.0. Its artifact configuration must reject extra artifacts and retain the original upstream signature on the embedded MSI. Change the version and matching checks together for each release.

ENABLE_SIGNPATH defaults to absent/false. Only approved client-v tags may request signing; pull requests and ordinary builds cannot. Do not add the API token to source or distribute it. A successful local build is unsigned and must never be described as signed. Returned signatures are validated before the signed artifact is uploaded.

After Foundation acceptance, publish the provider's required attribution on the homepage/download page: "Free code signing provided by SignPath.io, certificate by SignPath Foundation" with links to https://signpath.io and https://signpath.org. Do not present this sentence as a current affiliation before acceptance.

Privacy: see PRIVACY.md. The Client contacts the specified relay on launch and transfers session information after code-based pairing. Do not claim it is offline or sends no information.

Provider terms: https://signpath.org/terms.html
Workflow documentation: https://docs.signpath.io/trusted-build-systems/github


