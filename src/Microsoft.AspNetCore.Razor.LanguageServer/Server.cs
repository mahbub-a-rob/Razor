// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public abstract class Server
    {
        public abstract Task<GeneratedDocument> GenerateDocumentAsync(Project project, Document document);
    }
}
