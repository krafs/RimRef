using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RimRef.Generator;

// WaylithClr, the game's build of .NET, has a few APIs that the official .NET reference assemblies lack.
// They are added to copies of those reference assemblies the way the game's own assemblies see them: new types as part of System.Runtime, new members on the types that declare them.
internal static class WaylithClr
{
    private const string DiagnosticId = "WAYLITHCLR_SPECIFIC";
    private const string PackageDir = "waylithclr/net10.0";
    private static readonly string SdkDir = Path.Combine(AppContext.BaseDirectory, "sdk");

    public static List<PackageFile> CreateFiles(string gameDir)
    {
        using var coreLib = ReadGameModule(gameDir, "System.Private.CoreLib.dll");
        using var runtime = ReadSdkModule("System.Runtime.dll");
        using var intrinsics = ReadSdkModule("System.Runtime.Intrinsics.dll");
        var sdkTypes = IndexPublicTypes(runtime, intrinsics);

        var types = SelectTypes(gameDir, coreLib, sdkTypes);
        var methods = SelectMethods(coreLib, types);

        var copiers = new Dictionary<ModuleDefinition, ApiCopier>
        {
            [runtime] = new ApiCopier(runtime, sdkTypes),
            [intrinsics] = new ApiCopier(intrinsics, sdkTypes),
        };

        foreach (var type in types)
        {
            copiers[runtime].AddType(type);
        }

        foreach (var method in methods)
        {
            var declaringType = FindSdkType(sdkTypes, method.DeclaringType.FullName);
            ThrowIfNotForwarded(gameDir, declaringType);
            copiers[declaringType.Module].AddMethod(method, declaringType);
        }

        foreach (var copier in copiers.Values)
        {
            copier.Complete();
        }

        Validate(runtime, types, methods, sdkTypes);

        var typeNames = types.Select(t => t.FullName);
        var methodNames = methods.Select(m => $"{m.DeclaringType.FullName}.{m.Name}");
        var names = typeNames.Concat(methodNames);
        Console.WriteLine($"Added WaylithClr's APIs: {string.Join(", ", names)}");

        var runtimeBytes = Write(runtime);
        var intrinsicsBytes = Write(intrinsics);
        var targets = Package.ReadTemplate("Krafs.Rimworld.Ref.targets", "buildTransitive/net10.0/Krafs.Rimworld.Ref.targets");
        return
        [
            new PackageFile($"{PackageDir}/System.Runtime.dll", runtimeBytes),
            new PackageFile($"{PackageDir}/System.Runtime.Intrinsics.dll", intrinsicsBytes),
            ReadSdkFile("System.Runtime.xml"),
            ReadSdkFile("System.Runtime.Intrinsics.xml"),
            ReadSdkFile("LICENSE.TXT"),
            targets,
        ];
    }

    // The types the game's System.Runtime forwards that the official one lacks.
    private static List<TypeDefinition> SelectTypes(string gameDir, ModuleDefinition coreLib, Dictionary<string, TypeDefinition> sdkTypes)
    {
        using var facade = ReadGameModule(gameDir, "System.Runtime.dll");
        var types = new List<TypeDefinition>();
        foreach (var exportedType in facade.ExportedTypes)
        {
            if (exportedType.DeclaringType is not null || sdkTypes.ContainsKey(exportedType.FullName))
            {
                continue;
            }

            var type = coreLib.GetType(exportedType.FullName) ?? throw new GeneratorException($"The game's System.Runtime forwards {exportedType.FullName}, which its System.Private.CoreLib does not define.");
            if (!type.IsPublic)
            {
                continue;
            }

            // Anything else may be an official type the SDK keeps in another reference assembly, and copying it would make it ambiguous.
            var isWaylithClrType = type.Namespace == "WaylithClr" || type.Namespace.StartsWith("WaylithClr.");
            if (!isWaylithClrType)
            {
                throw new GeneratorException($"The game's System.Runtime forwards {type.FullName}, which is not in the WaylithClr namespace.");
            }

            types.Add(type);
        }

        return types;
    }

    // The members Ludeon adds to official types, which it marks as experimental with its own diagnostic ID.
    private static List<MethodDefinition> SelectMethods(ModuleDefinition coreLib, List<TypeDefinition> selectedTypes)
    {
        var methods = new List<MethodDefinition>();
        foreach (var type in coreLib.GetTypes())
        {
            var topLevelType = type;
            while (topLevelType.DeclaringType is not null)
            {
                topLevelType = topLevelType.DeclaringType;
            }

            if (selectedTypes.Contains(topLevelType))
            {
                continue;
            }

            var isMarked = IsMarked(type) || type.Fields.Any(IsMarked) || type.Properties.Any(IsMarked) || type.Events.Any(IsMarked);
            if (isMarked)
            {
                throw new GeneratorException($"{type.FullName} or one of its fields, properties or events is marked {DiagnosticId}, which is not supported.");
            }

            foreach (var method in type.Methods)
            {
                if (IsMarked(method) && IsApi(method))
                {
                    methods.Add(method);
                }
            }
        }

        return methods;
    }

    private static void Validate(ModuleDefinition runtime, List<TypeDefinition> types, List<MethodDefinition> methods, Dictionary<string, TypeDefinition> sdkTypes)
    {
        // The compiler only treats System.Runtime as the core library while it references no other assembly.
        if (runtime.AssemblyReferences.Count > 0)
        {
            throw new GeneratorException("WaylithClr's APIs made System.Runtime reference other assemblies.");
        }

        foreach (var type in types)
        {
            var copy = runtime.GetType(type.FullName);
            var expected = Describe(type);
            var actual = Describe(copy);
            ThrowIfDifferent(expected, actual);
        }

        foreach (var method in methods)
        {
            var declaringType = FindSdkType(sdkTypes, method.DeclaringType.FullName);
            var copy = declaringType.Methods.Single(m => m.FullName == method.FullName);
            var expected = Describe(method);
            var actual = Describe(copy);
            ThrowIfDifferent(expected, actual);
        }
    }

    private static void ThrowIfDifferent(List<string> expected, List<string> actual)
    {
        var missing = expected.Except(actual).FirstOrDefault();
        var unexpected = actual.Except(expected).FirstOrDefault();
        if (missing is not null || unexpected is not null)
        {
            throw new GeneratorException($"Copied WaylithClr API differs from the original. Missing: '{missing}'. Unexpected: '{unexpected}'.");
        }
    }

    // A mod's reference to a member resolves at runtime only if the game's facade of the same name forwards its type.
    private static void ThrowIfNotForwarded(string gameDir, TypeDefinition sdkType)
    {
        using var facade = ReadGameModule(gameDir, sdkType.Module.Name);
        var isForwarded = facade.ExportedTypes.Any(t => t.FullName == sdkType.FullName);
        if (!isForwarded)
        {
            throw new GeneratorException($"The game's {sdkType.Module.Name} does not forward {sdkType.FullName}.");
        }
    }

    private static Dictionary<string, TypeDefinition> IndexPublicTypes(params ModuleDefinition[] modules)
    {
        var types = new Dictionary<string, TypeDefinition>();
        foreach (var module in modules)
        {
            var publicTypes = module.GetTypes().Where(IsApi);
            foreach (var type in publicTypes)
            {
                types.Add(type.FullName, type);
            }
        }

        return types;
    }

    private static TypeDefinition FindSdkType(Dictionary<string, TypeDefinition> sdkTypes, string fullName)
    {
        return sdkTypes.GetValueOrDefault(fullName) ?? throw new GeneratorException($"{fullName} is not in the official reference assemblies.");
    }

    private static ModuleDefinition ReadGameModule(string gameDir, string fileName)
    {
        var path = Path.Combine(gameDir, fileName);
        return ModuleDefinition.ReadModule(path);
    }

    private static ModuleDefinition ReadSdkModule(string fileName)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(SdkDir);
        var parameters = new ReaderParameters { AssemblyResolver = resolver };
        var path = Path.Combine(SdkDir, fileName);
        return ModuleDefinition.ReadModule(path, parameters);
    }

    private static PackageFile ReadSdkFile(string fileName)
    {
        var path = Path.Combine(SdkDir, fileName);
        var content = File.ReadAllBytes(path);
        return new PackageFile($"{PackageDir}/{fileName}", content);
    }

    private static byte[] Write(ModuleDefinition module)
    {
        using var stream = new MemoryStream();
        var parameters = new WriterParameters { DeterministicMvid = true };
        module.Write(stream, parameters);
        return stream.ToArray();
    }

    private static bool IsMarked(ICustomAttributeProvider provider)
    {
        foreach (var attribute in provider.CustomAttributes)
        {
            if (attribute.AttributeType.Name == "ExperimentalAttribute" && attribute.ConstructorArguments[0].Value as string == DiagnosticId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsApi(TypeDefinition type)
    {
        return type.IsNested ? type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly : type.IsPublic;
    }

    private static bool IsApi(MethodDefinition method)
    {
        return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    // Value types keep their private instance fields, which affect how the compiler treats them.
    private static bool IsApi(FieldDefinition field)
    {
        return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly || (field.DeclaringType.IsValueType && !field.IsStatic);
    }

    private static bool IsApi(PropertyDefinition property)
    {
        return (property.GetMethod is not null && IsApi(property.GetMethod)) || (property.SetMethod is not null && IsApi(property.SetMethod));
    }

    private static bool IsApi(EventDefinition @event)
    {
        return (@event.AddMethod is not null && IsApi(@event.AddMethod)) || (@event.RemoveMethod is not null && IsApi(@event.RemoveMethod));
    }

    private static List<string> Describe(TypeDefinition type)
    {
        var layout = type.HasLayoutInfo ? $"pack={type.PackingSize} size={type.ClassSize}" : "";
        var attributes = Describe(type.CustomAttributes);
        var lines = new List<string> { $"type {type.FullName} {type.Attributes} {layout} : {type.BaseType?.FullName} {attributes}" };
        foreach (var implementation in type.Interfaces)
        {
            attributes = Describe(implementation.CustomAttributes);
            lines.Add($"interface {type.FullName} : {implementation.InterfaceType.FullName} {attributes}");
        }

        foreach (var field in type.Fields.Where(IsApi))
        {
            attributes = Describe(field.CustomAttributes);
            lines.Add($"field {field.FullName} {field.Attributes} offset={field.Offset} {field.Constant} {attributes}");
        }

        foreach (var method in type.Methods.Where(IsApi))
        {
            var methodLines = Describe(method);
            lines.AddRange(methodLines);
        }

        foreach (var property in type.Properties.Where(IsApi))
        {
            attributes = Describe(property.CustomAttributes);
            lines.Add($"property {property.FullName} {property.Attributes} get={property.GetMethod?.Name} set={property.SetMethod?.Name} {attributes}");
        }

        foreach (var @event in type.Events.Where(IsApi))
        {
            attributes = Describe(@event.CustomAttributes);
            lines.Add($"event {@event.FullName} {@event.Attributes} add={@event.AddMethod?.Name} remove={@event.RemoveMethod?.Name} {attributes}");
        }

        foreach (var nested in type.NestedTypes.Where(IsApi))
        {
            var nestedLines = Describe(nested);
            lines.AddRange(nestedLines);
        }

        return lines;
    }

    private static List<string> Describe(MethodDefinition method)
    {
        var flags = method.Attributes & ~MethodAttributes.PInvokeImpl;
        var attributes = Describe(method.CustomAttributes);
        var returnAttributes = Describe(method.MethodReturnType.CustomAttributes);
        var lines = new List<string> { $"method {method.FullName} {flags} {method.CallingConvention} {attributes} returns {returnAttributes}" };
        foreach (var parameter in method.Parameters)
        {
            attributes = Describe(parameter.CustomAttributes);
            lines.Add($"parameter {method.FullName} {parameter.Name} {parameter.Attributes} {parameter.Constant} {attributes}");
        }

        return lines;
    }

    private static string Describe(IEnumerable<CustomAttribute> attributes)
    {
        var descriptions = new List<string>();
        foreach (var attribute in attributes)
        {
            var arguments = attribute.ConstructorArguments.Select(Describe);
            var description = $"{attribute.AttributeType.FullName}({string.Join(", ", arguments)})";
            foreach (var named in attribute.Fields.Concat(attribute.Properties))
            {
                var value = Describe(named.Argument);
                description += $" {named.Name}={value}";
            }

            descriptions.Add(description);
        }

        descriptions.Sort(StringComparer.Ordinal);
        return string.Join("; ", descriptions);
    }

    private static string Describe(CustomAttributeArgument argument)
    {
        var value = argument.Value switch
        {
            TypeReference type => type.FullName,
            CustomAttributeArgument[] values => DescribeAll(values),
            CustomAttributeArgument boxed => Describe(boxed),
            var other => other?.ToString(),
        };
        return $"{argument.Type.FullName}:{value}";
    }

    private static string DescribeAll(CustomAttributeArgument[] values)
    {
        var descriptions = values.Select(Describe);
        return $"[{string.Join(", ", descriptions)}]";
    }

    // Copies definitions into an SDK reference assembly, mapping each type they mention to its definition in the SDK.
    private sealed class ApiCopier(ModuleDefinition module, Dictionary<string, TypeDefinition> sdkTypes)
    {
        private readonly Dictionary<string, TypeDefinition> _moduleTypes = module.GetTypes().ToDictionary(t => t.FullName);
        private readonly Dictionary<string, TypeDefinition> _copies = [];
        private readonly List<(TypeDefinition Source, TypeDefinition Copy)> _pendingTypes = [];
        private readonly List<(MethodDefinition Source, TypeDefinition DeclaringType)> _pendingMethods = [];

        public void AddType(TypeDefinition source)
        {
            var copy = CreateType(source);
            module.Types.Add(copy);
        }

        public void AddMethod(MethodDefinition source, TypeDefinition declaringType)
        {
            var exists = declaringType.Methods.Any(m => m.FullName == source.FullName);
            if (exists)
            {
                throw new GeneratorException($"{source.FullName} is marked {DiagnosticId}, but is already in the official reference assemblies.");
            }

            _pendingMethods.Add((source, declaringType));
        }

        // Members are copied once all types exist, since they can mention each other.
        public void Complete()
        {
            foreach (var (source, copy) in _pendingTypes)
            {
                CopyMembers(source, copy);
            }

            foreach (var (source, declaringType) in _pendingMethods)
            {
                CopyMethod(source, declaringType);
            }
        }

        private TypeDefinition CreateType(TypeDefinition source)
        {
            if (source.HasGenericParameters)
            {
                throw new GeneratorException($"{source.FullName} is generic, which is not supported.");
            }

            var ns = source.IsNested ? "" : source.Namespace;
            var copy = new TypeDefinition(ns, source.Name, source.Attributes);
            if (source.HasLayoutInfo)
            {
                copy.PackingSize = source.PackingSize;
                copy.ClassSize = source.ClassSize;
            }

            _copies[source.FullName] = copy;
            _pendingTypes.Add((source, copy));
            foreach (var nested in source.NestedTypes.Where(IsApi))
            {
                var nestedCopy = CreateType(nested);
                copy.NestedTypes.Add(nestedCopy);
            }

            return copy;
        }

        private void CopyMembers(TypeDefinition source, TypeDefinition copy)
        {
            copy.BaseType = source.BaseType is null ? null : Map(source.BaseType);
            CopyAttributes(source, copy);
            foreach (var implementation in source.Interfaces)
            {
                var interfaceType = Map(implementation.InterfaceType);
                var implementationCopy = new InterfaceImplementation(interfaceType);
                CopyAttributes(implementation, implementationCopy);
                copy.Interfaces.Add(implementationCopy);
            }

            foreach (var field in source.Fields.Where(IsApi))
            {
                var fieldType = Map(field.FieldType);
                var fieldCopy = new FieldDefinition(field.Name, field.Attributes, fieldType) { Offset = field.Offset };
                if (field.HasConstant)
                {
                    fieldCopy.Constant = field.Constant;
                }

                CopyAttributes(field, fieldCopy);
                copy.Fields.Add(fieldCopy);
            }

            var methods = new Dictionary<MethodDefinition, MethodDefinition>();
            foreach (var method in source.Methods.Where(IsApi))
            {
                methods[method] = CopyMethod(method, copy);
            }

            foreach (var property in source.Properties.Where(IsApi))
            {
                var getter = property.GetMethod is null ? null : methods.GetValueOrDefault(property.GetMethod);
                var setter = property.SetMethod is null ? null : methods.GetValueOrDefault(property.SetMethod);
                var propertyType = Map(property.PropertyType);
                var propertyCopy = new PropertyDefinition(property.Name, property.Attributes, propertyType) { GetMethod = getter, SetMethod = setter };
                CopyAttributes(property, propertyCopy);
                copy.Properties.Add(propertyCopy);
            }

            foreach (var @event in source.Events.Where(IsApi))
            {
                var adder = @event.AddMethod is null ? null : methods.GetValueOrDefault(@event.AddMethod);
                var remover = @event.RemoveMethod is null ? null : methods.GetValueOrDefault(@event.RemoveMethod);
                var eventType = Map(@event.EventType);
                var eventCopy = new EventDefinition(@event.Name, @event.Attributes, eventType) { AddMethod = adder, RemoveMethod = remover };
                CopyAttributes(@event, eventCopy);
                copy.Events.Add(eventCopy);
            }
        }

        private MethodDefinition CopyMethod(MethodDefinition source, TypeDefinition declaringType)
        {
            if (source.HasGenericParameters || source.HasOverrides || source.CallingConvention != MethodCallingConvention.Default)
            {
                throw new GeneratorException($"{source.FullName} is generic, overrides another method or has an unusual calling convention, which is not supported.");
            }

            var returnType = Map(source.ReturnType);
            var copy = new MethodDefinition(source.Name, source.Attributes & ~MethodAttributes.PInvokeImpl, returnType);
            declaringType.Methods.Add(copy);
            foreach (var parameter in source.Parameters)
            {
                var parameterType = Map(parameter.ParameterType);
                var parameterCopy = new ParameterDefinition(parameter.Name, parameter.Attributes, parameterType);
                if (parameter.HasConstant)
                {
                    parameterCopy.Constant = parameter.Constant;
                }

                CopyAttributes(parameter, parameterCopy);
                copy.Parameters.Add(parameterCopy);
            }

            CopyAttributes(source, copy);
            CopyAttributes(source.MethodReturnType, copy.MethodReturnType);

            // Like other reference assemblies: a body that only throws.
            if (!copy.IsAbstract)
            {
                var il = copy.Body.GetILProcessor();
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Throw);
            }

            return copy;
        }

        private void CopyAttributes(ICustomAttributeProvider source, ICustomAttributeProvider target)
        {
            foreach (var attribute in source.CustomAttributes)
            {
                var attributeType = Map(attribute.AttributeType);
                var voidType = Map(attribute.Constructor.ReturnType);
                var constructor = new MethodReference(".ctor", voidType, attributeType) { HasThis = true };
                foreach (var parameter in attribute.Constructor.Parameters)
                {
                    var parameterType = Map(parameter.ParameterType);
                    var parameterCopy = new ParameterDefinition(parameterType);
                    constructor.Parameters.Add(parameterCopy);
                }

                var copy = new CustomAttribute(constructor);
                foreach (var argument in attribute.ConstructorArguments)
                {
                    var argumentCopy = CopyArgument(argument);
                    copy.ConstructorArguments.Add(argumentCopy);
                }

                foreach (var field in attribute.Fields)
                {
                    var argumentCopy = CopyArgument(field.Argument);
                    var fieldCopy = new CustomAttributeNamedArgument(field.Name, argumentCopy);
                    copy.Fields.Add(fieldCopy);
                }

                foreach (var property in attribute.Properties)
                {
                    var argumentCopy = CopyArgument(property.Argument);
                    var propertyCopy = new CustomAttributeNamedArgument(property.Name, argumentCopy);
                    copy.Properties.Add(propertyCopy);
                }

                target.CustomAttributes.Add(copy);
            }
        }

        private CustomAttributeArgument CopyArgument(CustomAttributeArgument argument)
        {
            var type = Map(argument.Type);
            object? value = argument.Value switch
            {
                TypeReference typeValue => Map(typeValue),
                CustomAttributeArgument[] values => values.Select(CopyArgument).ToArray(),
                CustomAttributeArgument boxed => CopyArgument(boxed),
                var other => other,
            };
            return new CustomAttributeArgument(type, value);
        }

        private TypeReference Map(TypeReference type)
        {
            switch (type)
            {
                case GenericInstanceType generic:
                {
                    var elementType = Map(generic.ElementType);
                    var instance = new GenericInstanceType(elementType);
                    foreach (var argument in generic.GenericArguments)
                    {
                        var argumentCopy = Map(argument);
                        instance.GenericArguments.Add(argumentCopy);
                    }

                    return instance;
                }
                case ArrayType array:
                {
                    var elementType = Map(array.ElementType);
                    return new ArrayType(elementType, array.Rank);
                }
                case ByReferenceType byReference:
                {
                    var elementType = Map(byReference.ElementType);
                    return new ByReferenceType(elementType);
                }
                case PointerType pointer:
                {
                    var elementType = Map(pointer.ElementType);
                    return new PointerType(elementType);
                }
                case RequiredModifierType modified:
                {
                    var modifierType = Map(modified.ModifierType);
                    var elementType = Map(modified.ElementType);
                    return new RequiredModifierType(modifierType, elementType);
                }
                case OptionalModifierType modified:
                {
                    var modifierType = Map(modified.ModifierType);
                    var elementType = Map(modified.ElementType);
                    return new OptionalModifierType(modifierType, elementType);
                }
                case TypeSpecification or GenericParameter:
                {
                    throw new GeneratorException($"WaylithClr's API uses {type.FullName}, which is not supported.");
                }
            }

            if (_copies.TryGetValue(type.FullName, out var copy))
            {
                return copy;
            }

            // Includes the module's internal types, such as its own NullableAttribute.
            if (_moduleTypes.TryGetValue(type.FullName, out var moduleType))
            {
                return moduleType;
            }

            var sdkType = FindSdkType(sdkTypes, type.FullName);
            return module.ImportReference(sdkType);
        }
    }
}
