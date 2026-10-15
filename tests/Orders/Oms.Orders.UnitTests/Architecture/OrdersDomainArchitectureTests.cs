using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Oms.Orders.Api;
using Xunit;

namespace Oms.Orders.UnitTests.Architecture;

public sealed class OrdersDomainArchitectureTests
{
    private const string DomainNamespacePrefix = "Oms.Orders.Domain";

    private static readonly string[] _prohibitedNamespacePrefixes =
    [
        "Oms.Orders.Application",
        "Oms.Orders.Infrastructure",
        "Oms.Orders.Api",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "MediatR",
        "RabbitMQ",
    ];

    [Fact]
    public void DomainLayerShouldContainProductionTypesAndRemainInternal()
    {
        var assembly = typeof(Program).Assembly;
        var domainTypes = assembly.GetTypes()
            .Where(type => type.Namespace is not null &&
                           (type.Namespace == DomainNamespacePrefix ||
                            type.Namespace.StartsWith(DomainNamespacePrefix + ".", StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(domainTypes);
        Assert.All(domainTypes, type => Assert.False(type.IsPublic, $"Domain type '{type.FullName}' must be internal, not public."));
    }

    [Fact]
    public void DomainLayerSignaturesAndMethodBodiesShouldNotReferenceProhibitedLayersOrFrameworks()
    {
        var assemblyPath = typeof(Program).Assembly.Location;
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();

        var violations = new List<string>();

        foreach (var typeDefHandle in metadataReader.TypeDefinitions)
        {
            var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
            var typeNamespace = metadataReader.GetString(typeDef.Namespace);

            if (!IsDomainNamespace(typeNamespace))
            {
                continue;
            }

            var typeName = $"{typeNamespace}.{metadataReader.GetString(typeDef.Name)}";

            InspectBaseTypeAndInterfaces(metadataReader, typeDef, typeName, violations);
            InspectFields(metadataReader, typeDef, typeName, violations);
            InspectMethodsAndIlBodies(peReader, metadataReader, typeDef, typeName, violations);
        }

        Assert.Empty(violations);
    }

    private static bool IsDomainNamespace(string namespaceName) =>
        string.Equals(namespaceName, DomainNamespacePrefix, StringComparison.Ordinal) ||
        namespaceName.StartsWith(DomainNamespacePrefix + ".", StringComparison.Ordinal);

    private static bool IsProhibitedNamespace(string? namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return false;
        }

        foreach (var prefix in _prohibitedNamespacePrefixes)
        {
            if (string.Equals(namespaceName, prefix, StringComparison.Ordinal) ||
                namespaceName.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void InspectBaseTypeAndInterfaces(
        MetadataReader reader,
        TypeDefinition typeDef,
        string ownerTypeName,
        List<string> violations)
    {
        if (!typeDef.BaseType.IsNil)
        {
            CheckEntityHandle(reader, typeDef.BaseType, $"{ownerTypeName} (base type)", violations);
        }

        foreach (var interfaceImplHandle in typeDef.GetInterfaceImplementations())
        {
            var interfaceImpl = reader.GetInterfaceImplementation(interfaceImplHandle);
            CheckEntityHandle(reader, interfaceImpl.Interface, $"{ownerTypeName} (interface)", violations);
        }
    }

    private static void InspectFields(
        MetadataReader reader,
        TypeDefinition typeDef,
        string ownerTypeName,
        List<string> violations)
    {
        var typeProvider = new NamespaceCollectingTypeProvider(reader);

        foreach (var fieldHandle in typeDef.GetFields())
        {
            var fieldDef = reader.GetFieldDefinition(fieldHandle);
            var fieldName = reader.GetString(fieldDef.Name);
            var referencedNamespaces = fieldDef.DecodeSignature(typeProvider, genericContext: null);

            foreach (var ns in referencedNamespaces)
            {
                if (IsProhibitedNamespace(ns))
                {
                    violations.Add($"{ownerTypeName}.{fieldName} references prohibited namespace '{ns}'.");
                }
            }
        }
    }

    private static void InspectMethodsAndIlBodies(
        PEReader peReader,
        MetadataReader reader,
        TypeDefinition typeDef,
        string ownerTypeName,
        List<string> violations)
    {
        var typeProvider = new NamespaceCollectingTypeProvider(reader);

        foreach (var methodHandle in typeDef.GetMethods())
        {
            var methodDef = reader.GetMethodDefinition(methodHandle);
            var methodName = reader.GetString(methodDef.Name);
            var contextLabel = $"{ownerTypeName}.{methodName}";

            var signature = methodDef.DecodeSignature(typeProvider, genericContext: null);
            foreach (var ns in signature.ReturnType.Concat(signature.ParameterTypes.SelectMany(p => p)))
            {
                if (IsProhibitedNamespace(ns))
                {
                    violations.Add($"{contextLabel} (signature) references prohibited namespace '{ns}'.");
                }
            }

            if (methodDef.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var methodBody = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
            if (!methodBody.LocalSignature.IsNil)
            {
                var standaloneSig = reader.GetStandaloneSignature(methodBody.LocalSignature);
                foreach (var localNamespaces in standaloneSig.DecodeLocalSignature(typeProvider, genericContext: null))
                {
                    foreach (var ns in localNamespaces)
                    {
                        if (IsProhibitedNamespace(ns))
                        {
                            violations.Add($"{contextLabel} (local variable) references prohibited namespace '{ns}'.");
                        }
                    }
                }
            }

            InspectIlInstructions(reader, methodBody.GetILBytes(), contextLabel, violations);
        }
    }

    private static void InspectIlInstructions(
        MetadataReader reader,
        byte[]? ilBytes,
        string contextLabel,
        List<string> violations)
    {
        if (ilBytes is null || ilBytes.Length == 0)
        {
            return;
        }

        var index = 0;
        while (index < ilBytes.Length)
        {
            var opCode = ilBytes[index++];
            if (opCode == 0xFE)
            {
                if (index >= ilBytes.Length)
                {
                    break;
                }

                var secondByte = ilBytes[index++];
                index += GetTwoByteOpCodeOperandSize(secondByte);
                continue;
            }

            if (opCode == 0x45)
            {
                if (index + 4 > ilBytes.Length)
                {
                    break;
                }

                var caseCount = BitConverter.ToInt32(ilBytes, index);
                index += 4 + (caseCount * 4);
                continue;
            }

            if (IsMetadataTokenOpCode(opCode))
            {
                if (index + 4 > ilBytes.Length)
                {
                    break;
                }

                var token = BitConverter.ToInt32(ilBytes, index);
                index += 4;

                var handle = MetadataTokens.EntityHandle(token);
                if (!handle.IsNil)
                {
                    CheckEntityHandle(reader, handle, $"{contextLabel} (IL instruction)", violations);
                }

                continue;
            }

            index += GetSingleByteOpCodeOperandSize(opCode);
        }
    }

    private static void CheckEntityHandle(
        MetadataReader reader,
        EntityHandle handle,
        string contextLabel,
        List<string> violations)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                {
                    var td = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    CheckNamespace(reader.GetString(td.Namespace), contextLabel, violations);
                    break;
                }

            case HandleKind.TypeReference:
                {
                    var tr = reader.GetTypeReference((TypeReferenceHandle)handle);
                    CheckNamespace(reader.GetString(tr.Namespace), contextLabel, violations);
                    break;
                }

            case HandleKind.TypeSpecification:
                {
                    var ts = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
                    var namespaces = ts.DecodeSignature(new NamespaceCollectingTypeProvider(reader), genericContext: null);
                    foreach (var ns in namespaces)
                    {
                        CheckNamespace(ns, contextLabel, violations);
                    }

                    break;
                }

            case HandleKind.MemberReference:
                {
                    var mr = reader.GetMemberReference((MemberReferenceHandle)handle);
                    CheckEntityHandle(reader, mr.Parent, contextLabel, violations);
                    break;
                }

            case HandleKind.MethodDefinition:
                {
                    var md = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    var declaringType = reader.GetTypeDefinition(md.GetDeclaringType());
                    CheckNamespace(reader.GetString(declaringType.Namespace), contextLabel, violations);
                    break;
                }

            case HandleKind.FieldDefinition:
                {
                    var fd = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                    var declaringType = reader.GetTypeDefinition(fd.GetDeclaringType());
                    CheckNamespace(reader.GetString(declaringType.Namespace), contextLabel, violations);
                    break;
                }

            case HandleKind.MethodSpecification:
                {
                    var ms = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    CheckEntityHandle(reader, ms.Method, contextLabel, violations);
                    var namespaces = ms.DecodeSignature(new NamespaceCollectingTypeProvider(reader), genericContext: null);
                    foreach (var ns in namespaces.SelectMany(n => n))
                    {
                        CheckNamespace(ns, contextLabel, violations);
                    }

                    break;
                }
        }
    }

    private static void CheckNamespace(string namespaceName, string contextLabel, List<string> violations)
    {
        if (IsProhibitedNamespace(namespaceName))
        {
            violations.Add($"{contextLabel} references prohibited namespace '{namespaceName}'.");
        }
    }

    private static bool IsMetadataTokenOpCode(byte opCode) =>
        opCode is 0x28 or 0x29 or 0x27 or 0x6F or 0x70 or 0x71 or 0x73 or 0x74 or 0x75 or 0x79
            or 0x7B or 0x7C or 0x7D or 0x7E or 0x7F or 0x80 or 0x81 or 0x8C or 0x8D or 0x8F
            or 0xA3 or 0xA4 or 0xA5 or 0xC2 or 0xC6 or 0xD0;

    private static int GetSingleByteOpCodeOperandSize(byte opCode) =>
        opCode switch
        {
            0x0E or 0x0F or 0x10 or 0x11 or 0x12 or 0x13 or 0x1F or 0x2B or 0x2C or 0x2D or 0x2E or 0x2F
                or 0x30 or 0x31 or 0x32 or 0x33 or 0x34 or 0x35 or 0x36 or 0x37 or 0xDE => 1,
            0x20 or 0x22 or 0x38 or 0x39 or 0x3A or 0x3B or 0x3C or 0x3D or 0x3E or 0x3F or 0x40 or 0x41
                or 0x42 or 0x43 or 0x44 or 0x72 or 0xDD => 4,
            0x21 or 0x23 => 8,
            _ => 0,
        };

    private static int GetTwoByteOpCodeOperandSize(byte secondByte) =>
        secondByte switch
        {
            0x18 or 0x19 => 1,
            0x09 or 0x0A or 0x0B or 0x0C or 0x0D or 0x0E => 2,
            0x06 or 0x07 or 0x15 or 0x16 or 0x1C or 0x1D or 0x1E => 4,
            _ => 0,
        };

    private sealed class NamespaceCollectingTypeProvider(MetadataReader reader)
        : ISignatureTypeProvider<IReadOnlyList<string>, object?>
    {
        public IReadOnlyList<string> GetPrimitiveType(PrimitiveTypeCode typeCode) => [];

        public IReadOnlyList<string> GetTypeFromDefinition(MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var td = reader.GetTypeDefinition(handle);
            return [reader.GetString(td.Namespace)];
        }

        public IReadOnlyList<string> GetTypeFromReference(MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var tr = reader.GetTypeReference(handle);
            return [reader.GetString(tr.Namespace)];
        }

        public IReadOnlyList<string> GetTypeFromSpecification(MetadataReader metadataReader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public IReadOnlyList<string> GetSZArrayType(IReadOnlyList<string> elementType) => elementType;

        public IReadOnlyList<string> GetArrayType(IReadOnlyList<string> elementType, ArrayShape shape) => elementType;

        public IReadOnlyList<string> GetByReferenceType(IReadOnlyList<string> elementType) => elementType;

        public IReadOnlyList<string> GetPointerType(IReadOnlyList<string> elementType) => elementType;

        public IReadOnlyList<string> GetGenericInstantiation(IReadOnlyList<string> genericType, System.Collections.Immutable.ImmutableArray<IReadOnlyList<string>> typeArguments) =>
            genericType.Concat(typeArguments.SelectMany(t => t)).ToArray();

        public IReadOnlyList<string> GetGenericMethodParameter(object? genericContext, int index) => [];

        public IReadOnlyList<string> GetGenericTypeParameter(object? genericContext, int index) => [];

        public IReadOnlyList<string> GetModifiedType(IReadOnlyList<string> modifier, IReadOnlyList<string> unmodifiedType, bool isRequired) =>
            modifier.Concat(unmodifiedType).ToArray();

        public IReadOnlyList<string> GetPinnedType(IReadOnlyList<string> elementType) => elementType;

        public IReadOnlyList<string> GetFunctionPointerType(MethodSignature<IReadOnlyList<string>> signature) =>
            signature.ReturnType.Concat(signature.ParameterTypes.SelectMany(p => p)).ToArray();
    }
}
