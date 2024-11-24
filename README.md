# Gain - Dentrix Adapter

## Overview

The Dentrix software suite is meant to be installed on a cluster of machines.
*Exactly one* of the nodes is the designated **Dentrix Server**. It is
responsible for managing the database and scheduling/coordinating changes that
other nodes on the network need. *At least one* of the nodes is a **Dentrix
Workstation**. This is the machine hosting the main user-facing Dentrix
application. It queries the server on the network for most operations.

For many small practices, it's probably the case that a single computer serves
as both the Dentrix Server and Workstation. In larger offices, we might instead
see multiple different workstations connected to some Dentrix Server running on
Windows Server. In this case, the Dentrix Server isn't expected to ever be
connected to directly.

3P plugins like the one we're building *must* have some component that exists
on a Dentrix Workstation. This is because connecting to the Dentrix Server is
done through a function in `Dentrix.API.dll` (provided by Dentrix) that
launches a GUI for additional authentication. This would inevitably fail on
platforms without a user interface like Windows Server. That said, this is a
one-time process. The function registers a new user into the local Dentrix
database and returns a connection string. The returned connection string can be
passed around for use by any other executable without issue.

With this context in mind, we can think about how we could choose to design the
Gain adapter. This adapter is meant to be a proxy for queries, forwarding SQL
statements sent by the Gain servers to the local Dentrix instance.

In the ideal situation, we have an installer that runs on a Dentrix Workstation
and obtains a connection string. This connection string can then be passed to
our adapter that lives on the same node as the Dentrix Server. In this way, the
adapter will run as long as the master node of the cluster is running. This
approach also introduces complications though - its harder to test services
that span a network or that need to work on multiple operating systems (even if
they are all Windows variants).

In a less ideal situation, we could instead have our adapter installed onto a
Dentrix Workstation in the same way any other traditional application would.
The problem here though is that when the workstation is offline, so is our
adapter. Worse, if the installing user were to just logout, that would also
render our adapter useless.

So we compromise. The Gain adapter has two components - a user-facing
application and a
[Windows Service](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service).
Both are installed onto a Dentrix Workstation but the service lives outside the
domain of users, existing (and running) on the machine even if a different (or
no) user is logged in. The application exists solely to bootstrap the service
with an API key and the Dentrix connection string. Once that is set, the
service can ideally run indefinitely without intervention. The main caveat is
that an internet connection needs to be maintained on the machine. To
summarize:

- `GainService` is a Windows service that maintains a websocket connection to
  the Gain backend. Queries are received along this connection and proxied
  to/from the locally installed Dentrix instance.
- `GainApp` is a small [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/?view=netdesktop-9.0)-based
  project used to update API keys, initiate the connection to Dentrix, and
  check (at a very high-level) the state of the locally running `GainService`.

Separately:

- `CodeSigningCertificate` is a standalone directory containing a copy of the
  executable we need to sign with our EV code signing certificate. This signed
  executable is sent to the Dentrix team for registration. Refer to the README
  found within that directory for more details.
- The `DentrixDlg` project hosts custom actions used to find where Dentrix is
  installed. These CAs are used by the `GainPackage` project.
- `GainPackage` is a [WiX](https://wixtoolset.org/) project defining our `.msi`
  installer. The resulting installation script adds the necessary registry
  keys, boots and configures our service, installs all signed .dlls and .exes,
  etc.
- `GainInstaller` is a WiX project defining our bootstrapper application. This
  checks for (and installs if necessary) the .NET runtimes necessary to run our
  app. Afterwards it invokes the `.msi` installer once finished.

The actual process of code signing happens through GitHub CI. Refer to the
`dentrix-adapter.yml` workflow for details. Keep in mind you can only connect
to Dentrix through a signed application. This unfortunately makes the dev cycle
pretty slow.

## Development

At the top-level of this repository exist multiple **projects** created using
[Visual Studio](https://visualstudio.microsoft.com/):

- `GainApp`
- `GainService`
- `DentrixDlg`
- `GainPackage`
- `GainInstaller`

Within each of these projects exists another README that dives deeper into how
they work. Alongside these projects exists the `FluidicML.sln` file. This is
the **solution**, a small XML file containing descriptions of the various
projects.

Also found within these projects are `.csproj` or `.wixproj` files. The latter
is discussed in the `GainPackage` project's README. The former contains MSBuild
XML code that gets executed when calling `Build` from within Visual Studio or
by calling `MSBuild.exe` from the command line. Alternatively, we can also
choose to run the `dotnet` command. This is a thin wrapper around `MSBuild.exe`
available in later versions of the NET SDK (which we use).

### .NET

Speaking of `dotnet`, let's talk about the [.NET](https://learn.microsoft.com/en-us/dotnet/core/introduction)
ecosystem. The term ".NET" is used equivalently to refer to a few different
concepts including:

- the underlying runtime/platform;
- related frameworks;
- the various compilers;
- etc.

To keep things clear, we attempt to disambiguate overlapping uses by using
".NET" as an adjective describing some other concept, e.g. the .NET runtime.
Most commonly, the C# language is used to interact with the .NET runtime. It is
the only language (outside of XML) used in this repository.

You may see the following around on the web:

- `.NET Framework`
  * The original, Windows-only, iteration of the .NET platform.
- `.NET Core`
  * The cross-platform successor to `.NET Framework`.
- `.NET Standard`
  * A separate platform intended to be compatible with both `.NET Framework`
    and `.NET Core`.
- `.NET`
  * Another name for later versions of `.NET Core`.

All of these just refer to different versions of the .NET runtime. Why they
went with such a confusing naming scheme is beyond me. This
[link](https://stackoverflow.com/a/76748398) is useful for explaining a bit
more in depth.

Generally speaking, we target `.NET`/`.NET Core` since they are the latest
version of the runtime. This isn't possible when we need dependencies only
available in earlier versions.

### Builds

Because later versions of the .NET runtime are cross-platform, it is our
responsibility to tell MSBuild (directly, through Visual Studio, through the
`dotnet` command, etc.) how the project should be compiled. We do so through a
multitude of different configuration options.

The three most important options, and the ones that actually designate how
project outputs (e.g. `.dll`s and `.exe`s) are compiled and run, are the
[TargetFramework](https://learn.microsoft.com/en-us/dotnet/standard/frameworks),
[PlatformTarget](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-target-framework-and-target-platform?view=vs-2022#target-platform),
and
[RuntimeIdentifier](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog).

#### TargetFramework

The `TargetFramework` specifies which version of the .NET platform the project
should build against and run on. In other words, it specifies the APIs that
should be made available to the project. For example, we specify
`net8.0-windows` in `Gain.csproj` and `GainService.csproj` since we need access
to Windows-based .NET APIs (used to run a Windows GUI and service
respectively). In contrast, our `DentrixDlg.csproj` file specifies a `net472`
`TargetFramework` since this particular framework is necessary for use by our
installer.

> `net472` is a .NET Framework library meaning it only works on Windows. That's
> why the `net472` moniker doesn't have a `-windows` suffix like the other two
> mentioned.

#### PlatformTarget

The `PlatformTarget` specifies the architecture being built against. For
example, if we want to run the solution on 32-bit Intel processors, we specify
a `PlatformTarget` of `x86`. 64-bit Intel processors instead use `x64`. It's up
to us to build all combinations of `TargetFramework` and `PlatformTarget`
needed by our end users.

> There also exists an `AnyCPU` `PlatformTarget` value. This lets our
> application run in 64-bit mode when possible and 32-bit mode otherwise (the
> power of an abstracted .NET runtime). This sounds ideal, but we do **not**
> want to use this. Dentrix dependencies may be 32- or 64-bit. If the .NET
> runtime decides to run our executables with an incompatible bit mode, our
> processes will fail.

#### RuntimeIdentifier

The `RuntimeIdentifier` is usually some moniker consisting of a target OS and
architecture. Examples include `linux-x64`, `win-x64`, and `osx-x64`. These
values are used by [NuGet](https://www.nuget.org/), the .NET platform's
preferred dependency manager, to decide what variations of packages should be
linked. This is because certain packages may require e.g. unmanaged DLLs or
native code to perform functionality. For example, an encryption library could
depend on `openssl` on Linux but `CryptoAPI` on Windows.

While the other two properties are specified to `dotnet` or `MSBuild.exe` with
the `--property` command line argument, this property can be specified with
either the `--runtime` or `--arch` arguments. Read about their differences
[here](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build).

#### Project Configurations

To muddy matters, .NET projects also have a concept of `Configuration` and
`Platform` (not to be confused with `PlatformTarget`). `Configuration` is
usually something like `Debug` or `Release` whereas `Platform` is usually
something like `x86` or `x64`. Together they form a so-called project
configuration. But keep in mind, `Platform` is *just a name* used to
disambiguate what set of configurations you want to use. It doesn't really mean
anything.

For example, I could define a "Release x86" project configuration (a
`Configuration` of `Release` and a `Platform` of `x86`) that sets the
`PlatformTarget` property to `x86`. But I could've also defined "Release x86"
to (confusingly) set the `PlatformTarget` to `x64`. I could also define a
project configuration called "Release blahblahblah".

Use of a project configuration can be convenient since they can be used to
switch between values the other three properties may be set to. Furthermore,
the defaults are sensible. For example, `Release` configurations tend to have
properties set that enable optimization and strip out debugging symbols.
Ultimately, if we choose to use a project configuration for builds, we must
also make sure the configuration is set appropriately. It's probably easiest to
do that within Visual Studio.

## Release

To build a signed version of the solution according to the `Staging`
configuration, push to the `staging` branch. The `Release` configuration is
targeted on the `prod` branch. Once the GitHub CI action completes, the
installers (x86) and (x64) will be available as artifacts.
