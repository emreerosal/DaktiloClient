# Privacy and access

Launching DaktiloClient initiates a connection to its configured relay (currently158.220.98.93:45892) to obtain a temporary connection code. The relay sees network IP addresses, connection timing and pairing metadata. After an operator uses the code, screen frames, remote-input events, selected files and USB traffic traverse the relay. The application uses TLS certificate pinning and session encryption; the relay distributes and knows the pairing key, so it is a trusted party rather than a cryptographically excluded third party.

The person holding the code can connect without another approval dialog. File access can include local drives; it is not limited to a dedicated exchange folder. USB sharing permits device traffic. Keyboard and mouse control can be turned off in the visible window. Stop ends the Client session. The application itself contains no analytics SDK, ad system or session-recording implementation, but this does not establish that a support operator or service infrastructure cannot record information.

Missing USB components are prepared using the embedded original usbipd-win MSI, which can install drivers, a Windows service and configuration. Windows asks for elevation. Local installer logs and extracted setup files can remain in the user's temporary directory. See UNINSTALL.md.

The source code does not establish the production relay operator's log retention, backup policy, jurisdiction or contact address. The owner must publish that operational information and official contact on the homepage before public distribution/application; no retention guarantee is invented here.

Use only on a computer/device you own or are authorized to support. Do not share your code with an unexpected caller. Closing this Client does not uninstall installed USB drivers, stop an independently used system USB service or revoke persistent upstream USB bindings.
