# Gain - Dentrix Adapter

## Overview

This is the Windows application for proxying queries sent from the Gain backend
and query results returned from the locally installed Dentrix instance. Though
our adapter is intended to be small, there are a few moving parts necessary to
get things working:

- The `BackgroundService` is a [Windows Service](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
  that maintains a websocket connection to the Gain backend. Queries are
  received along this connection and proxied to/from the locally installed
  Dentrix instance.
- The `Application` is a small WPF-based project useful for updating API keys
  and checking (at a very high-level) the state of the `BackgroundService`.
- The `Installer` is a [WiX](https://wixtoolset.org/) project defining our
  installer. The resulting installation script adds the necessary registry
  keys, boots and configures our service, installs all signed .dlls and .exes,
  etc.

Separately we also have:

- The `CodeSigningCertificate` is a standalone directory containing a copy of
  the executable we need to sign with our EV code signing certificate. This
  signed executable is sent to the Dentrix team for registration. Refer to the
  README found within that directory for more details.
- The `DentrixDlg` project hosts custom actions used to find where Dentrix is
  installed. These custom actions (CAs) are meant to be used by the
  `DentrixDlg` defined within the `Installer` project.

Code signing happens through GitHub CI. Refer to the `dentrix-adapter.yml`
workflow for details. Keep in mind you can only connect to Dentrix through
a signed application. This unfortunately makes the dev cycle pretty slow.
