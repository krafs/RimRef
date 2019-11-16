# RimRef

### What is this?
RimRef is a NuGet package containing reference assemblies for all managed C# assemblies in RimWorld.

### What does it do?
It allows for compiling RimWorld mods without the game assemblies.

### Who is it for?
Anyone making C# mods for RimWorld.

### Why use it?
By removing the need for adding references to actual game files, developers can make their mods self-contained.
A self-contained mod has everything needed to build it contained within the mod project itself.
Basically, anyone can clone the git repository of a self-contained mod, open it in her IDE, click Build, and it will work. No need to manually add any references*.

### Where can I get it?
It is available on [nuget.org](https://www.nuget.org/packages/Krafs.Rimworld.Ref).
Using Visual Studio - Add it to your mod project by right-clicking **Dependencies** or **References** -> **Manage NuGet packages**. 
Tick the *Include prerelease* checkbox, search for **Krafs.Rimworld.Ref** and install.__**__.
 -Don't forget to remove the old references to the actual game assemblies!

Published with permission from Ludeon Studios.

[Ludeon Forums thread](https://ludeon.com/forums/index.php?topic=49851.0).

## In-depth

### What are *reference assemblies*?
There are two kinds of assemblies: *Implementation assemblies* and *reference assemblies*.

*Implementation assemblies* are simply full, regular assemblies used in applications. 

*Reference assemblies* derive from implementation assemblies, and contain the exact same public metadata, but all actual logic has been removed. All methods are empty. Calling a method in a reference assembly will yield a runtime exception. But the compiler only cares about the metadata, so we can use reference assemblies to build our mods. By default, these assemblies are only referenced during build, and not put into the output directory of the project.

### Why not distribute the *implementation assemblies* instead?
Firstly, reference assemblies are much smaller in size, which makes the package much lighter.

Secondly, Ludeon Studios has permitted this package to be published - on the condition that the assemblies contain only metadata.

---

__*__* Obviously, you could make a mod self-contained by storing the actual game assemblies along with your code. That would yield the exact same result. However, doing so in a publicly accessible repository violates the RimWorld EULA, which explicitly states that "you can't distribute anything we've made unless we agree to it".
Of course, if you don't keep your code in a public repository this is less of an issue. And all the reasons why you should keep your source code public is the topic for another time :)

__**__ This package is only compatible with projects using ´PackageReference´. It's a different, much better way to organize your NuGet references than ´packages.config´.
However, it is easy to migrate your project from ´packages.config´ to ´PackageReference´. See [this guide](https://docs.microsoft.com/en-us/nuget/consume-packages/migrate-packages-config-to-package-reference).
