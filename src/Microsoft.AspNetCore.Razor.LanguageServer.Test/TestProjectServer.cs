// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class TestProjectServer : Server
    {
        public static readonly IEnumerable<MetadataReference> DefaultReferences;

        static TestProjectServer()
        {
            var context = DependencyContext.Load(typeof(TestProjectServer).GetTypeInfo().Assembly);
            DefaultReferences = context.CompileLibraries
                    .SelectMany(lib => lib.ResolveReferencePaths())
                    .Select(path => AssemblyMetadata.CreateFromFile(path).GetReference())
                    .ToList();
        }

        public static Scope Create(
            string projectName = "TestRazorProject",
            IEnumerable<MetadataReference> metadataReferences = null)
        {
            if (metadataReferences == null)
            {
                metadataReferences = DefaultReferences;
            }

            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(projectName),
                VersionStamp.Default,
                name: projectName,
                assemblyName: projectName,
                language: LanguageNames.CSharp,
                metadataReferences: metadataReferences));

            return new Scope(workspace, project.Id, new TestProjectServer());
        }

        private TestProjectServer()
        {
        }

        public async override Task<GeneratedDocument> GenerateDocumentAsync(Project project, Document document)
        {
            var p = await ProjectLoader.LoadAsync(project);
            return null;
        }

        public class Scope
        {
            public Scope(Workspace workspace, ProjectId projectId, Server server)
            {
                Workspace = workspace;
                ProjectId = projectId;
                Server = server;
            }

            public ProjectId ProjectId { get; }

            public Server Server { get; }

            public Workspace Workspace { get; }
        }
    }
}
