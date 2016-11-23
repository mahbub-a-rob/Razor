// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Evolution
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class RazorEngineDependencyAttribute : Attribute
    {
        public RazorEngineDependencyAttribute(string assemblyName)
        {
            if (assemblyName == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}
