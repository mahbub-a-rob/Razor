// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class ProjectLoader
    {
        public static async Task<ProjectServer> LoadAsync(Project project)
        {
            var compilation = await project.GetCompilationAsync();
            var assemblies = GetRazorCustomizationAssemblies(compilation);
            return new ProjectServer(compilation, assemblies);
        }

        private static IEnumerable<AssemblyIdentity> GetRazorCustomizationAssemblies(Compilation compilation)
        {
            var assemblies = new HashSet<AssemblyIdentity>();

            foreach (var identity in compilation.ReferencedAssemblyNames)
            {
                if (identity.Name == "Microsoft.AspNetCore.Razor.Evolution")
                {
                    assemblies.Add(identity);
                    break;
                }
            }

            if (assemblies.Count == 0)
            {
                // This doesn't reference Razor.
                return assemblies;
            }

            var dependencies = new List<string>();
            foreach (var reference in compilation.References)
            {
                var assembly = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (assembly == null)
                {
                    continue;
                }

                AssemblyIdentity identity;
                var name = assembly.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!AssemblyIdentity.TryParseDisplayName(name, out identity))
                {
                    continue;
                }

                var attributes = assembly.GetAttributes();
                foreach (var attribute in attributes)
                {
                    if (string.Equals(
                        "Microsoft.AspNetCore.Razor.Evolution.RazorEngineCustomizationAttribute",
                        attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        StringComparison.Ordinal))
                    {
                        assemblies.Add(identity);
                    }

                    if (string.Equals(
                        "Microsoft.AspNetCore.Razor.Evolution.RazorEngineDependencyAttribute",
                        attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        StringComparison.Ordinal))
                    {
                        dependencies.Add((string)attribute.ConstructorArguments[0].Value);
                    }
                }
            }

            foreach (var dependency in dependencies)
            {
                AssemblyIdentity candidate;
                if (AssemblyIdentity.TryParseDisplayName(dependency, out candidate))
                {
                    foreach (var identity in compilation.ReferencedAssemblyNames)
                    {
                        if (AssemblyIdentityComparer.Default.ReferenceMatchesDefinition(candidate, identity))
                        {
                            assemblies.Add(identity);
                            break;
                        }
                    }
                }
            }

            return assemblies;
        }
    }
}
