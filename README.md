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

## Versions
RimRef's version is the game's version: RimRef `1.6.4871` contains the reference assemblies of RimWorld `1.6.4871`.

Versions built from the game's unstable branch on Steam end in `-unstable`, such as `1.7.4981-unstable`. They are prereleases, so your IDE or `dotnet add package` only shows them when you include prereleases.

To always get the latest build of a game version, use a wildcard such as `1.6.*`, or `1.7.*-*` to include prereleases.

## What's included
RimRef contains every managed assembly the game ships, including private members, so [publicizers](https://github.com/krafs/Publicizer) work with it.

From RimWorld 1.7, .NET itself is not included. Your project gets it from the .NET SDK, like any other .NET project. RimWorld runs on Ludeon's own build of .NET, and the few APIs it adds aren't included.

## How it's made
A [workflow](https://github.com/krafs/RimRef/blob/main/.github/workflows/make-and-upload-package.yml) downloads RimWorld from Steam twice a day, and the [generator](https://github.com/krafs/RimRef/tree/main/src/RimRef.Generator) turns its assemblies into a package. If that version isn't on [NuGet.org](https://www.nuget.org/packages/Krafs.Rimworld.Ref) yet, it's uploaded.

---
Published with permission from Ludeon Studios.
