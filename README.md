# RimRef

### What is this?
RimRef is a NuGet package containing Reference Assemblies* for all managed C# assemblies in RimWorld.

### What does it do?
It allows for compiling RimWorld mods without the game assemblies.

### Who is it for?
Anyone making C# mods for RimWorld.

### Why use it?
By removing the need for adding references to actual game files, developers can make their mods self-contained.
A self-contained mod has everything needed to build it contained within the mod project itself. This is convenient, but can also be particularly useful when collaborating on mods with other people, as everyone can just all download the source code, click Build, and it will compile. No need having to setup the solution with their own dependencies.

### Where can I get it?
It is available on [nuget.org](https://www.nuget.org/packages/Krafs.Rimworld.Ref).
Using Visual Studio - Add it to your mod project by right-clicking **Dependencies** -> **Manage NuGet packages**. 

Search for **Krafs.Rimworld.Ref** and install.__**__.


---

### * Reference Assemblies

These are reference assemblies. That means that all methods in the assemblies have been stripped of code, leaving only their signatures. However, the signatures are all the compiler needs to build your mod. One of the most common reasons this is used in development in general is to make the NuGet packages smaller - making downloads of big packages faster, and take up less space. In the case of Rimworld it more importantly allows us to distribute "Rimworld's game files" without actually leaking the source code. Well, except the signatures, but that ended up being an ok compromise.

Published with permission from Ludeon Studios.

__**__ This package is only compatible with projects using PackageReference. 
By default when you create a new .NET472-project for a Rimworld mod, you are given a project set up for the old packages.config-structure. Trying to reference krafs.rimworld.ref in a project that uses packages.config usually causes an error message. To fix this you need to migrate to using PackageReferences for the project. It is very easy. See [this guide](https://docs.microsoft.com/en-us/nuget/consume-packages/migrate-packages-config-to-package-reference).
