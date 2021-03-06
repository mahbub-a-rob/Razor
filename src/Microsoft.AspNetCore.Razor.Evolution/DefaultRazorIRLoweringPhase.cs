﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Evolution.Intermediate;
using Microsoft.AspNetCore.Razor.Evolution.Legacy;

namespace Microsoft.AspNetCore.Razor.Evolution
{
    internal class DefaultRazorIRLoweringPhase : RazorEnginePhaseBase, IRazorIRLoweringPhase
    {
        protected override void ExecuteCore(RazorCodeDocument codeDocument)
        {
            var syntaxTree = codeDocument.GetSyntaxTree();
            ThrowForMissingDependency(syntaxTree);

            var visitor = new Visitor();

            visitor.VisitBlock(syntaxTree.Root);

            var irDocument = (DocumentIRNode)visitor.Builder.Build();
            codeDocument.SetIRDocument(irDocument);
        }

        private class Visitor : ParserVisitor
        {
            private readonly Stack<RazorIRBuilder> _builders;

            public Visitor()
            {
                _builders = new Stack<RazorIRBuilder>();
                var document = RazorIRBuilder.Document();
                _builders.Push(document);

                Namespace = new NamespaceDeclarationIRNode();
                Builder.Push(Namespace);

                Class = new ClassDeclarationIRNode();
                Builder.Push(Class);

                Method = new RazorMethodDeclarationIRNode();
                Builder.Push(Method);
            }

            public RazorIRBuilder Builder => _builders.Peek();

            public NamespaceDeclarationIRNode Namespace { get; }

            public ClassDeclarationIRNode Class { get; }

            public RazorMethodDeclarationIRNode Method { get; }

            public override void VisitStartAttributeBlock(AttributeBlockChunkGenerator chunkGenerator, Block block)
            {
                var value = new ContainerRazorIRNode();
                Builder.Add(new HtmlAttributeIRNode()
                {
                    Name = chunkGenerator.Name,
                    Prefix = chunkGenerator.Prefix,
                    Value = value,
                    Suffix = chunkGenerator.Suffix,

                    SourceLocation = block.Start,
                });

                var valueBuilder = RazorIRBuilder.Create(value);
                _builders.Push(valueBuilder);
            }

            public override void VisitEndAttributeBlock(AttributeBlockChunkGenerator chunkGenerator, Block block)
            {
                _builders.Pop();
            }

            public override void VisitStartDynamicAttributeBlock(DynamicAttributeBlockChunkGenerator chunkGenerator, Block block)
            {
                var content = new ContainerRazorIRNode();
                Builder.Add(new CSharpAttributeValueIRNode()
                {
                    Prefix = chunkGenerator.Prefix,
                    Content = content,
                    SourceLocation = block.Start,
                });

                var valueBuilder = RazorIRBuilder.Create(content);
                _builders.Push(valueBuilder);
            }

            public override void VisitEndDynamicAttributeBlock(DynamicAttributeBlockChunkGenerator chunkGenerator, Block block)
            {
                _builders.Pop();
            }

            public override void VisitLiteralAttributeSpan(LiteralAttributeChunkGenerator chunkGenerator, Span span)
            {
                Builder.Add(new HtmlAttributeValueIRNode()
                {
                    Prefix = chunkGenerator.Prefix,
                    Content = chunkGenerator.Value,
                    SourceLocation = span.Start,
                });
            }

            public override void VisitStartTemplateBlock(TemplateBlockChunkGenerator chunkGenerator, Block block)
            {
                Builder.Push(new TemplateIRNode());
            }

            public override void VisitEndTemplateBlock(TemplateBlockChunkGenerator chunkGenerator, Block block)
            {
                Builder.Pop();
            }

            // CSharp expressions are broken up into blocks and spans because Razor allows Razor comments
            // inside an expression.
            // Ex:
            //      @DateTime.@*This is a comment*@Now
            //
            // We need to capture this in the IR so that we can give each piece the correct source mappings
            public override void VisitStartExpressionBlock(ExpressionChunkGenerator chunkGenerator, Block block)
            {
                var value = new ContainerRazorIRNode();
                Builder.Add(new CSharpExpressionIRNode()
                {
                    Content = value,
                    SourceLocation = block.Start,
                });

                var valueBuilder = RazorIRBuilder.Create(value);
                _builders.Push(valueBuilder);
            }

            public override void VisitEndExpressionBlock(ExpressionChunkGenerator chunkGenerator, Block block)
            {
                _builders.Pop();
            }

            public override void VisitExpressionSpan(ExpressionChunkGenerator chunkGenerator, Span span)
            {
                Builder.Add(new CSharpTokenIRNode()
                {
                    Content = span.Content,
                    SourceLocation = span.Start,
                });
            }

            public override void VisitTypeMemberSpan(TypeMemberChunkGenerator chunkGenerator, Span span)
            {
                var functionsNode = new CSharpStatementIRNode()
                {
                    Content = span.Content,
                    SourceLocation = span.Start,
                    Parent = Class,
                };

                Class.Children.Add(functionsNode);
            }

            public override void VisitStatementSpan(StatementChunkGenerator chunkGenerator, Span span)
            {
                Builder.Add(new CSharpStatementIRNode()
                {
                    Content = span.Content,
                    SourceLocation = span.Start,
                });
            }

            public override void VisitMarkupSpan(MarkupChunkGenerator chunkGenerator, Span span)
            {
                var currentChildren = Builder.Current.Children;
                if (currentChildren.Count > 0 && currentChildren[currentChildren.Count - 1] is HtmlContentIRNode)
                {
                    var existingHtmlContent = (HtmlContentIRNode)currentChildren[currentChildren.Count - 1];
                    existingHtmlContent.Content = string.Concat(existingHtmlContent.Content, span.Content);
                }
                else
                {
                    Builder.Add(new HtmlContentIRNode()
                    {
                        Content = span.Content,
                        SourceLocation = span.Start,
                    });
                }
            }

            public override void VisitImportSpan(AddImportChunkGenerator chunkGenerator, Span span)
            {
                // For prettiness, let's insert the usings before the class declaration.
                var i = 0;
                for (; i < Namespace.Children.Count; i++)
                {
                    if (Namespace.Children[i] is ClassDeclarationIRNode)
                    {
                        break;
                    }
                }

                var @using = new UsingStatementIRNode()
                {
                    Content = span.Content,
                    Parent = Namespace,
                    SourceLocation = span.Start,
                };

                Namespace.Children.Insert(i, @using);
            }

            public override void VisitDirectiveToken(DirectiveTokenChunkGenerator chunkGenerator, Span span)
            {
                Builder.Add(new DirectiveTokenIRNode()
                {
                    Content = span.Content,
                    Descriptor = chunkGenerator.Descriptor,
                    SourceLocation = span.Start,
                });
            }

            public override void VisitStartDirectiveBlock(DirectiveChunkGenerator chunkGenerator, Block block)
            {
                Builder.Push(new DirectiveIRNode()
                {
                    Name = chunkGenerator.Descriptor.Name,
                    Descriptor = chunkGenerator.Descriptor,
                });
            }

            public override void VisitEndDirectiveBlock(DirectiveChunkGenerator chunkGenerator, Block block)
            {
                Builder.Pop();
            }

            private class ContainerRazorIRNode : RazorIRNode
            {
                private SourceLocation? _location;

                public override IList<RazorIRNode> Children { get; } = new List<RazorIRNode>();

                public override RazorIRNode Parent { get; set; }

                internal override SourceLocation SourceLocation
                {
                    get
                    {
                        if (_location == null)
                        {
                            if (Children.Count > 0)
                            {
                                return Children[0].SourceLocation;
                            }

                            return SourceLocation.Undefined;
                        }

                        return _location.Value;
                    }
                    set
                    {
                        _location = value;
                    }
                }

                public override void Accept(RazorIRNodeVisitor visitor)
                {
                    visitor.VisitDefault(this);
                }

                public override TResult Accept<TResult>(RazorIRNodeVisitor<TResult> visitor)
                {
                    return visitor.VisitDefault(this);
                }
            }
        }
    }
}
