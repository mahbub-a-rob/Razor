// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class ProjectServer
    {
        private readonly IEnumerable<AssemblyIdentity> _assemblies;
        private readonly Compilation _compilation;
        private readonly Project _project;

        public ProjectServer(Project project, Compilation compilation, IEnumerable<AssemblyIdentity> assemblies)
        {
            _project = project;
            _compilation = compilation;
            _assemblies = assemblies;
        }

        public void Load()
        {
            foreach (var )
        }
    }
}
