# Installer

## Overview

This is a [WiX](https://wixtooslet.org/) project defining our installer. As
such, it maintains project references to all the other top-level projects in
this repository.

WiX is useful in streamlining definitions of many common MSBuild installation
concepts. Unfortunately, documentation for the WiX toolset varies from poor to
non-existent. In many cases, you'll instead want to refer to
[MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview?view=vs-2022)'s
documentation directly.

The top-level `.wixproj` file is a project file variation with support for WiX
specific tags. Documentation is unsurprisingly sparse. It also doesn't appear
to be intended for direct modification in the same way the `.csproj` files
found in the other projects are so, for the most part, just ignore it. The rest
of the files are either `.wxs` files specifying different pieces of an MSBuild
or `.wxi` files specifying reusable variables.
