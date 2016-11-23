// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    public class ProjectServerTest
    {
        [Fact]
        public void CreateServer()
        {
            var scope = TestProjectServer.Create();
        }

        [Fact]
        public async Task CreateServer_AndLoadHost()
        {
            var scope = TestProjectServer.Create();

            var project = scope.Workspace.CurrentSolution.GetProject(scope.ProjectId);
            await scope.Server.GenerateDocumentAsync(project, null);
        }
    }
}
