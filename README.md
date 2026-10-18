# RimRef <a href="https://www.nuget.org/packages/Krafs.Rimworld.Ref"><img alt="Version" src="https://img.shields.io/nuget/vpre/Krafs.Rimworld.Ref?label=Latest"></a>  <a href="https://www.nuget.org/packages/Krafs.Rimworld.Ref"><img alt="Downloads" src="https://img.shields.io/nuget/dt/Krafs.Rimworld.Ref?label=Downloads"></a>

RimRef is a NuGet package of reference assemblies for RimWorld. Reference it in your mod project instead of the game's own assemblies, and your mod builds anywhere: on another machine, in CI, or without RimWorld installed.

Reference assemblies contain only the game's signatures, not its code. That's all the compiler needs, and it's what lets RimRef be distributed when the game can't be.

## Getting started
Reference RimRef in your mod's project file, and target the framework of the RimWorld version you build for:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Krafs.Rimworld.Ref" Version="1.7.*-*" />
  </ItemGroup>

</Project>
```

| RimWorld | Target framework |
|---|---|
| 1.7 | `net10.0` |
| 1.1 - 1.6 | `net472` |
| 1.0 | `net35` |

## Versioning
RimRef's version mirrors the game's version: RimRef `1.6.4871` contains the reference assemblies of RimWorld `1.6.4871`.

Versions built from the game's unstable branch on Steam end in `-unstable`, such as `1.7.4981-unstable`. They are prereleases, so your package manager only shows them when you include prereleases.

If RimRef itself needs a fix, the same game version gets a number at the end: `1.7.4981.1`, or `1.7.4981-unstable.1` for prereleases.

To always use the latest build of a game version, use a wildcard such as `1.6.*`, or `1.7.*-*` to include prereleases.

## What's included
RimRef contains the game's managed assemblies, including private members, so [publicizers](https://github.com/krafs/Publicizer) work with them.

Up to RimWorld 1.6, that includes the game's own .NET assemblies. From 1.7, .NET is not included: your project gets it from the .NET SDK, like any other .NET project.

The SDK lacks two things the game's .NET has: private members, so .NET types can't be publicized, and the APIs Ludeon adds in its own build of .NET, such as the `WaylithClr` namespace. Reach either through reflection or [`UnsafeAccessor`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute). More on why in [RimWorld 1.7 and .NET](https://github.com/krafs/RimRef/discussions/108).

## How it's made
A [workflow](https://github.com/krafs/RimRef/blob/main/.github/workflows/make-and-upload-package.yml) downloads RimWorld from Steam twice a day, and turns its assemblies into a package. If that version isn't on [NuGet.org](https://www.nuget.org/packages/Krafs.Rimworld.Ref) yet, it's uploaded.

---
Published with permission from Ludeon Studios.
