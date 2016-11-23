// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class AssemblyLoader
    {
        public bool TryResolveAssembly(Project project, AssemblyIdentity identity, out string path)
        {
            foreach (var reference in project.AnalyzerReferences)
            {
                as
            }
        }
    }
}
