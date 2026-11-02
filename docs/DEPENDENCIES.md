# Dependencies

- Main Client source: MIT, see LICENSE. .NET Framework/Windows APIs are system dependencies. The included logo/icon bytes remain unchanged; no trademark endorsement is granted.
- usbipd-win5.3.0: upstream https://github.com/dorssel/usbipd-win/tree/v5.3.0, GPL-3.0-only with upstream notices. Original MSI, source archive and COPYING are in third-party and embedded by build.ps1. Setup is a separate elevated process. We do not claim to be upstream or replace its signature.
- The upstream source's Drivers/README.md identifies VirtualBox7.2.2 GPLv3 drivers and preserves its source-offer information pointing to https://download.virtualbox.org/virtualbox/. That exact upstream notice is also copied as third-party/DRIVERS-README.md. Review upstream obligations before public distribution; no third-party license is replaced by the MIT license on Client code.
- GitHub Actions checkout/upload are build infrastructure only; SignPath action is only used after provider configuration and approval. They are not embedded in the EXE.

The Client does not contain the administrator receiver package, its source archive or its driver installer. No credentials, private keys, signing tokens, relay deployment scripts or account files are required by the source build.
