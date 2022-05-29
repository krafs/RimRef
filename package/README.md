# RimRef
RimRef is a NuGet-package that contains reference assemblies for Rimworld. You reference it in your mod project instead of the game.

This makes your project portable, because RimRef can be downloaded by anyone and used from anywhere, unlike Rimworld's assemblies which can't be distributed.

## FAQ
### Why do I get an error when trying to build with RimRef?
Likely because you are adding it to a project that uses an [old package structure](https://docs.microsoft.com/en-us/nuget/reference/packages-config). Fear not! It is easy to fix. Do one of the following:
- [Migrate](https://docs.microsoft.com/en-us/nuget/consume-packages/migrate-packages-config-to-package-reference) your existing project
- Create a new project library for **.NET 5 or later**, and replace the `<TargetFramework>`-value in the project file with `net472`.

### What are 'reference assemblies'?
Reference assemblies are ordinary assemblies from which all code has been stripped, leaving only their signatures. However, these signatures are all the compiler needs to build your project. Not only does this speed up the build process, it more importantly offers a way to reference Rimworld from anywhere without illegally distributing any of its content.

---
Published with permission from Ludeon Studios.
