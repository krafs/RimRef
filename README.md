# RimRef <a href="https://www.nuget.org/packages/Krafs.Rimworld.Ref"><img alt="Version" src="https://img.shields.io/nuget/vpre/Krafs.Rimworld.Ref?label=Latest"></a>  <a href="https://www.nuget.org/packages/Krafs.Rimworld.Ref"><img alt="Downloads" src="https://img.shields.io/nuget/dt/Krafs.Rimworld.Ref?label=Downloads"></a>

RimRef is a NuGet-package that contains reference assemblies for Rimworld. You reference it in your mod project instead of the game.

This makes your project portable, because RimRef can be downloaded by anyone and used from anywhere, unlike Rimworld's assemblies which can't be distributed.

## Installation
Install RimRef (Krafs.Rimworld.Ref) from your IDE's package manager, or with [dotnet](https://docs.microsoft.com/en-us/dotnet/core/install):

```shell
dotnet add package Krafs.Rimworld.Ref
```

## FAQ
### - Why do I get an error when trying to build with RimRef?
Likely because you are adding it to a project that uses an [old package structure](https://docs.microsoft.com/en-us/nuget/reference/packages-config). Fear not! It is easy to fix. Do one of the following:
- [Migrate](https://docs.microsoft.com/en-us/nuget/consume-packages/migrate-packages-config-to-package-reference) your existing project
- Create a new project library for **.NET 5 or later**, and replace the `<TargetFramework>`-value in the project file with `net472`.

### - What are 'reference assemblies'?
Reference assemblies are ordinary assemblies from which all code has been stripped, leaving only their signatures. However, these signatures are all the compiler needs to build your project. Not only does this speed up the build process, it more importantly offers a way to reference Rimworld from anywhere without illegally distributing any of its content.

### - How is this package made?
It is automatically generated everytime Rimworld updates. See [this script](https://github.com/krafs/RimRef/blob/main/.github/workflows/make-and-upload-package.yml) for details.

In short, the script downloads the Rimworld assemblies from Steam every day, and checks if the game's version has changed. If it has, the new reference assemblies are generated and uploaded in a package to [NuGet.org](https://www.nuget.org/packages/Krafs.Rimworld.Ref "Krafs.Rimworld.Ref").

RimRef's version mirrors Rimworld's version, meaning RimRef version `v1.2.3` contains reference assemblies for Rimworld version `v1.2.3`.

The script also generates pre-releases from some Steam beta-branches, in which case `-beta` is appended to the version, e.g. `v1.2.3-beta`.

---
Published with permission from Ludeon Studios.
