﻿// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Evolution
{
    public static class DirectiveDescriptorBuilder
    {
        public static IDirectiveDescriptorBuilder Create(string name)
        {
            return new DefaultDirectiveDescriptorBuilder(name, DirectiveDescriptorKind.SingleLine);
        }

        public static IDirectiveDescriptorBuilder CreateRazorBlock(string name)
        {
            return new DefaultDirectiveDescriptorBuilder(name, DirectiveDescriptorKind.RazorBlock);
        }

        public static IDirectiveDescriptorBuilder CreateCodeBlock(string name)
        {
            return new DefaultDirectiveDescriptorBuilder(name, DirectiveDescriptorKind.CodeBlock);
        }

        private class DefaultDirectiveDescriptorBuilder : IDirectiveDescriptorBuilder
        {
            private readonly List<DirectiveTokenDescriptor> _tokenDescriptors;
            private readonly string _name;
            private readonly DirectiveDescriptorKind _type;

            public DefaultDirectiveDescriptorBuilder(string name, DirectiveDescriptorKind type)
            {
                _name = name;
                _type = type;
                _tokenDescriptors = new List<DirectiveTokenDescriptor>();
            }

            public IDirectiveDescriptorBuilder AddType()
            {
                var descriptor = new DirectiveTokenDescriptor()
                {
                    Kind = DirectiveTokenKind.Type
                };
                _tokenDescriptors.Add(descriptor);

                return this;
            }

            public IDirectiveDescriptorBuilder AddMember()
            {
                var descriptor = new DirectiveTokenDescriptor()
                {
                    Kind = DirectiveTokenKind.Member
                };
                _tokenDescriptors.Add(descriptor);

                return this;
            }

            public IDirectiveDescriptorBuilder AddString()
            {
                var descriptor = new DirectiveTokenDescriptor()
                {
                    Kind = DirectiveTokenKind.String
                };
                _tokenDescriptors.Add(descriptor);

                return this;
            }

            public IDirectiveDescriptorBuilder AddLiteral(string literal)
            {
                var descriptor = new DirectiveTokenDescriptor()
                {
                    Kind = DirectiveTokenKind.Literal,
                    Value = literal,
                };
                _tokenDescriptors.Add(descriptor);

                return this;
            }

            public DirectiveDescriptor Build()
            {
                var descriptor = new DirectiveDescriptor
                {
                    Name = _name,
                    Kind = _type,
                    Tokens = _tokenDescriptors,
                };

                return descriptor;
            }
        }
    }
}
