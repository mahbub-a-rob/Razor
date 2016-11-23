// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Evolution
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class RazorEngineCustomizationAttribute : Attribute
    {
        public RazorEngineCustomizationAttribute(string typeFullName, string methodName)
        {
            if (typeFullName == null)
            {
                throw new ArgumentNullException(nameof(typeFullName));
            }

            if (methodName == null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            TypeFullName = typeFullName;
            MethodName = methodName;
        }

        public string TypeFullName { get; }

        public string MethodName { get; }
    }
}
