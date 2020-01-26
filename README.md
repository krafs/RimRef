# RimRef

### What is this?
RimRef is a NuGet package containing reference assemblies for all managed C# assemblies in RimWorld.

### What does it do?
It allows for compiling RimWorld mods without the game assemblies.

### Who is it for?
Anyone making C# mods for RimWorld.

### Why use it?
By removing the need for adding references to actual game files, developers can make their mods self-contained.
A self-contained mod has everything needed to build it contained within the mod project itself. This is useful in that you never have to worry about finding your Rimworld assemblies when setting up a new mod project, or when moving an existing project to a different computer.

### Where can I get it?
It is available on [nuget.org](https://www.nuget.org/packages/Krafs.Rimworld.Ref).
Using Visual Studio - Add it to your mod project by right-clicking **Dependencies** -> **Manage NuGet packages**. 

Search for **Krafs.Rimworld.Ref** and install.__**__.

 -Don't forget to remove any old references to actual game assemblies!

Published with permission from Ludeon Studios.

[Ludeon Forums thread](https://ludeon.com/forums/index.php?topic=49851.0).

---

## Note
These are not the assembly files from the game. They have been _generated_ from the game, but can not be run. Decompiling these assemblies will show all method bodies as empty. However, modders generally only need to reference the files when compiling code - not when running it.


__**__ This package is only compatible with projects using PackageReference. It's a different, much better way to organize your NuGet references than packages.config.

Migrating your project from packages.config to PackageReference is easy. See [this guide](https://docs.microsoft.com/en-us/nuget/consume-packages/migrate-packages-config-to-package-reference).
