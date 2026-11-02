# Build provenance and scope

The original Client-only shipped artifact was inspected statically with REA: SHA256913a4679f4705baae9a2c09d8284d7ca0d97dda989160032f44e906fdc4c0b54. Missing authored UI sources were reconstructed from that project's own managed artifact using dnSpy.Console. Recovered sources were then separated and simplified to host-only code. Available authored transport, adaptive-capture, file-session and startup sources were preserved directly. There is no need to run any decompiler or use a prebuilt Client EXE to build this release.

Removed from the source-build Client: outbound viewer UI, receiver dashboard, file-transfer administration window, outbound application join path and USB receiver attach entry point. The Client retains code to service remote requests and its necessary session protocol. Public Client source necessarily describes Client-side interoperability; this does not open the separate receiver application's source.

Brand assets were extracted unchanged from the Client resource, not regenerated. Original logo SHA256050f6696dd4e50a46ced0d954164cf29ed09f316eba25953ec8fb24f0669119a.

The embedded original usbipd-win5.3.0 MSI has SHA2561c984914aec944de19b64eff232421439629699f8138e3ddc29301175bc6d938 and retained a valid upstream Authenticode signature during preparation. The upstream source archive and license notices are included. No dependency is re-signed by the Client build.

Framework compiler output need not be byte-identical across environments/build timestamps. Signing origin is an audited source build, not an assertion of deterministic binary identity.
