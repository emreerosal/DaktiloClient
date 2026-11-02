# System changes and removal

The main Client is portable: it does not register itself for Windows startup or install its own background service. Delete DaktiloClient.exe after closing it to remove that portable application.

The USB host is a separate persistent Windows component. If it is missing, the Client starts its original MSI using Windows elevation. The installer can add usbipd-win drivers, the usbipd service and a firewall rule. The Client's installer wrapper disables newly created rules named usbipd; it does not broadly disable Windows Firewall or change pre-existing rules. Existing installations may be started without reinstalling. USB bind requests from an active support session may require another elevation prompt and can persist beyond the session.

To remove USB components that were installed for this application:

1. Stop the session and close the Client. Finish device operations first.
2. If usbipd-win was already installed or is used by WSL/other tools, coordinate with its owner; do not remove a shared dependency without approval.
3. Otherwise open Windows Settings > Apps > Installed apps, locate usbipd-win / USBIP Device Host and uninstall using its registered uninstaller. Windows may require administrator approval or restart.
4. For persistent device sharing configuration, inspect the installed version's `usbipd list` and `usbipd --help`; the operator can unbind only the device they explicitly intend to stop sharing. No blanket unbind is performed by this project.
5. Installer files/logs may remain in MRDaktilo-USB-* temporary directories. Review and delete only the specific setup directory once installation has completed; do not remove arbitrary temporary folders.

Stop in the app terminates its tunnel; it does not promise removal of independently installed USB services, drivers, firewall rules or bindings. Documentation is provided instead of an automatic destructive cleanup action.
