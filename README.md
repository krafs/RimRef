# RimRef

### What does it do?
*RimRef* allows for compiling *RimWorld* mods without the game assemblies.

### Who is it for?
Anyone making C# mods for RimWorld.

### What is it?
It is a NuGet package containing reference assemblies for all managed C# assemblies in RimWorld.

### Why use it?
By removing the need for adding references to actual game files, developers can make their mods **self-contained**.

A **self-contained** mod has everything needed to build it contained within the mod project itself.
Basically, I can clone the git repository of a mod, open it in my IDE, click *Build*, and it will work. No need to manually add any references.

Obviously, you could make a mod self-contained by storing the actual game assemblies along with your code. That would yield the exact same result. However, doing so in a publicly accessible repository violates the RimWorld EULA, which explicitly states that "*you can't distribute anything we've made unless we agree to it*".

### What are *reference assemblies*?
There are two kinds of assemblies: *Implementation assemblies* and *reference assemblies*.

*Implementation assemblies* are simply full, regular assemblies used in applications. 

*Reference assemblies* derive from implementation assemblies, and contain the exact same public metadata, but all actual logic has been removed. All methods are empty. Calling a method in a reference assembly will yield a runtime exception. But the compiler only cares about the metadata, so we can use reference assemblies to build our mods.

### Why not distribute the *implementation assemblies* instead?
Firstly, reference assemblies are much smaller in size, which makes the package much lighter.

Secondly, Ludeon Studios has permitted this package to be published - on the condition that the assemblies contain only metadata.
