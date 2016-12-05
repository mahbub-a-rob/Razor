// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Evolution.Intermediate;
using Microsoft.AspNetCore.Razor.Evolution.Legacy;

namespace Microsoft.AspNetCore.Razor.Evolution
{
    internal class DefaultRazorCSharpLoweringPhase : RazorEnginePhaseBase, IRazorCSharpLoweringPhase
    {
        protected override void ExecuteCore(RazorCodeDocument codeDocument)
        {
            var irDocument = codeDocument.GetIRDocument();
            ThrowForMissingDependency(irDocument);

            var csharpDocument = new RazorCSharpDocument();

            var csharpRenderers = Engine.Features.OfType<ICSharpRenderer>();
            
            var rendererOrchestrator = new CSharpRendererOrchestrator(csharpRenderers);
            csharpDocument = rendererOrchestrator.Render(irDocument);

            codeDocument.SetCSharpDocument(csharpDocument);
        }

        public class CSharpRenderingContext
        {
            public ICollection<DirectiveDescriptor> Directives { get; set; }

            public Action<IList<RazorIRNode>> Render { get; set; }

            public CSharpCodeWriter Writer { get; set; }

            public ICollection<RazorError> Errors { get; } = new List<RazorError>();
        }

        public class InjectDirectiveRenderer : ICSharpRenderer
        {
            private readonly string _injectAttribute;

            public InjectDirectiveRenderer(string injectAttribute)
            {
                _injectAttribute = $"[{injectAttribute}]";
            }

            public RazorEngine Engine { get; set; }

            public int Order { get; } = 1;

            public bool TryRender(RazorIRNode node, CSharpRenderingContext context)
            {
                if (node is DirectiveIRNode)
                {
                    var directiveNode = (DirectiveIRNode)node;
                    if (directiveNode.Name == "inject")
                    {
                        var typeName = ((DirectiveTokenIRNode)directiveNode.Children[0]).Content;
                        var memberName = ((DirectiveTokenIRNode)directiveNode.Children[1]).Content;

                        context.Writer
                            .WriteLine(_injectAttribute)
                            .Write("public global::")
                            .Write(typeName)
                            .Write(" ")
                            .Write(memberName)
                            .WriteLine(" { get; private set; }");
                    }
                }

                return false;
            }
        }

        public class DesignTimeCSharpRenderer : ICSharpRenderer
        {
            private const string DesignTimeVariable = "__o";
            private const string ActionHelper = "__actionHelper";

            private readonly RazevolutionPaddingBuilder _paddingBuilder;

            public RazorEngine Engine { get; set; }

            public int Order { get; }

            public DesignTimeCSharpRenderer(RazorEngineHost host)
            {
                _paddingBuilder = new RazevolutionPaddingBuilder(host);
            }

            public bool TryRender(ICSharpSource source, CSharpRenderingContext context)
            {
                // TODO: Need to handle generic directives and render appropriate design time code to color them.

                if (source is CSharpSource)
                {
                    Render((CSharpSource)source, context);
                }
                else if (source is RenderExpression)
                {
                    Render((RenderExpression)source, context);
                }
                else if (source is RenderTagHelper)
                {
                    Render((RenderTagHelper)source, context);
                }
                else if (source is InitializeTagHelperStructure)
                {
                    Render((InitializeTagHelperStructure)source, context);
                }
                else if (source is CreateTagHelper)
                {
                    Render((CreateTagHelper)source, context);
                }
                else if (source is SetTagHelperProperty)
                {
                    Render((SetTagHelperProperty)source, context);
                }
                else if (source is ImportNamespace)
                {
                    Render((ImportNamespace)source, context);
                }
                else if (source is RenderConditionalAttribute)
                {
                    Render((RenderConditionalAttribute)source, context);
                }
                else if (source is ConditionalAttributePiece)
                {
                    Render((ConditionalAttributePiece)source, context);
                }
                else if (source is RenderSection)
                {
                    Render((RenderSection)source, context);
                }
                else if (source is RenderStatement)
                {
                    Render((RenderStatement)source, context);
                }
                else if (source is ExecuteMethodDeclaration)
                {
                    Render((ExecuteMethodDeclaration)source, context);

                    // We can't fully render the execute method declaration. Let another CSharpRenderer do it.
                    return false;
                }
                else if (source is DeclareTagHelperFields)
                {
                    Render((DeclareTagHelperFields)source, context);
                }
                else if (source is Template)
                {
                    Render((Template)source, context);
                }
                else
                {
                    return false;
                }

                return true;
            }

            private void Render(IRazorDirective source, CSharpRenderingContext context)
            {
                const string TypeHelper = "__typeHelper";

                for (var i = 0; i < source.Tokens.Count; i++)
                {
                    var token = source.Tokens[i];
                    var tokenType = token.Descriptor.Type;

                    if (token.DocumentLocation == null ||
                        (tokenType != RazorDirectiveTokenType.Type &&
                        tokenType != RazorDirectiveTokenType.Member &&
                        tokenType != RazorDirectiveTokenType.String))
                    {
                        continue;
                    }

                    // Wrap the directive token in a lambda to isolate variable names.
                    context.Writer.WriteStartAssignment(ActionHelper);
                    using (context.Writer.BuildLambda(endLine: true))
                    {
                        switch (tokenType)
                        {
                            case RazorDirectiveTokenType.Type:
                                using (context.Writer.BuildCodeMapping(token.DocumentLocation))
                                {
                                    context.Writer.Write(token.Value);
                                }

                                context.Writer
                                    .Write(" ")
                                    .WriteStartAssignment(TypeHelper)
                                    .WriteLine("null;");
                                break;
                            case RazorDirectiveTokenType.Member:
                                context.Writer
                                    .Write(typeof(object).FullName)
                                    .Write(" ");

                                using (context.Writer.BuildCodeMapping(token.DocumentLocation))
                                {
                                    context.Writer.Write(token.Value);
                                }
                                context.Writer.WriteLine(" = null;");
                                break;
                            case RazorDirectiveTokenType.String:
                                context.Writer
                                    .Write(typeof(object).FullName)
                                    .Write(" ")
                                    .WriteStartAssignment(TypeHelper);

                                if (token.Value.StartsWith("\"", StringComparison.Ordinal))
                                {
                                    using (context.Writer.BuildCodeMapping(token.DocumentLocation))
                                    {
                                        context.Writer.Write(token.Value);
                                    }
                                }
                                else
                                {
                                    context.Writer.Write("\"");
                                    using (context.Writer.BuildCodeMapping(token.DocumentLocation))
                                    {
                                        context.Writer.Write(token.Value);
                                    }
                                    context.Writer.Write("\"");
                                }

                                context.Writer.WriteLine(";");
                                break;
                        }
                    }
                }
            }

            private void Render(CSharpSource source, CSharpRenderingContext context)
            {
                context.Writer.Write(source.Code);
            }

            private void Render(RenderExpression source, CSharpRenderingContext context)
            {
                if (source.Expression.Children.Count == 0)
                {
                    return;
                }

                if (source.DocumentLocation != null)
                {
                    using (context.Writer.BuildLinePragma(source.DocumentLocation))
                    using (context.Writer.NoIndent())
                    {
                        var paddingString = BuildAssignmentOffsetPadding(source.Padding);

                        context.Writer
                            .Write(paddingString)
                            .WriteStartAssignment(DesignTimeVariable);

                        using (context.Writer.BuildCodeMapping(source.DocumentLocation))
                        {
                            context.Render(source.Expression.Children);
                        }

                        context.Writer.WriteLine(";");
                    }
                }
                else
                {
                    context.Writer.WriteStartAssignment(DesignTimeVariable);
                    context.Render(source.Expression.Children);
                    context.Writer.WriteLine(";");
                }
            }

            private void Render(ImportNamespace source, CSharpRenderingContext context)
            {
                context.Writer.WriteUsing(source.Namespace);
            }

            private void Render(RenderConditionalAttribute source, CSharpRenderingContext context)
            {
                context.Render(source.ValuePieces);
            }

            private void Render(ConditionalAttributePiece source, CSharpRenderingContext context)
            {
                context.Render(source.Value.Children);
            }

            private void Render(RenderSection source, CSharpRenderingContext context)
            {
                const string SectionWriterName = "__razor_section_writer";

                context.Writer
                    .WriteStartMethodInvocation(context.CodeLiterals.DefineSectionMethodName)
                    .WriteStringLiteral(source.Name)
                    .WriteParameterSeparator();

                var redirectConventions = new CSharpRedirectRenderingConventions(SectionWriterName, context);
                using (context.UseRenderingConventions(redirectConventions))
                using (context.Writer.BuildAsyncLambda(endLine: false, parameterNames: SectionWriterName))
                {
                    context.Render(source.Children);
                }

                context.Writer.WriteEndMethodInvocation();
            }

            private void Render(RenderStatement source, CSharpRenderingContext context)
            {
                Debug.Assert(source.Code != null);

                if (source.DocumentLocation != null)
                {
                    using (context.Writer.BuildLinePragma(source.DocumentLocation))
                    using (context.Writer.NoIndent())
                    {
                        var paddingString = _paddingBuilder.BuildPaddingString(source.Padding);

                        context.Writer.Write(paddingString);

                        using (context.Writer.BuildCodeMapping(source.DocumentLocation))
                        {
                            context.Writer.Write(source.Code);
                        }
                    }
                }
                else
                {
                    context.Writer.WriteLine(source.Code);
                }
            }

            private void Render(ExecuteMethodDeclaration source, CSharpRenderingContext context)
            {
                const string DesignTimeHelperMethodName = "__RazorDesignTimeHelpers__";
                const int DisableVariableNamingWarnings = 219;

                context.Writer
                    .Write("private static object @")
                    .Write(DesignTimeVariable)
                    .WriteLine(";");

                using (context.Writer.BuildMethodDeclaration("private", "void", "@" + DesignTimeHelperMethodName))
                {
                    using (context.Writer.BuildDisableWarningScope(DisableVariableNamingWarnings))
                    {
                        context.Writer.WriteVariableDeclaration(typeof(Action).FullName, ActionHelper, value: null);

                        var directives = context.GetDirectives();
                        foreach (var directive in directives)
                        {
                            Render(directive, context);
                        }
                    }
                }
            }

            private void Render(Template source, CSharpRenderingContext context)
            {
                const string ItemParameterName = "item";
                const string TemplateWriterName = "__razor_template_writer";

                context.Writer
                    .Write(ItemParameterName).Write(" => ")
                    .WriteStartNewObject(context.CodeLiterals.TemplateTypeName);

                var redirectConventions = new CSharpRedirectRenderingConventions(TemplateWriterName, context);
                using (context.UseRenderingConventions(redirectConventions))
                using (context.Writer.BuildAsyncLambda(endLine: false, parameterNames: TemplateWriterName))
                {
                    context.Render(source.Children);
                }

                context.Writer.WriteEndMethodInvocation();
            }

            private void Render(ViewClassDeclaration source, CSharpRenderingContext context)
            {
                context.Writer
                    .Write(source.Accessor)
                    .Write(" class ")
                    .Write(source.Name);

                if (source.BaseTypeName != null || source.ImplementedInterfaceNames != null)
                {
                    context.Writer.Write(" : ");
                }

                if (source.BaseTypeName != null)
                {
                    context.Writer.Write(source.BaseTypeName);

                    if (source.ImplementedInterfaceNames != null)
                    {
                        context.Writer.WriteParameterSeparator();
                    }
                }

                if (source.ImplementedInterfaceNames != null)
                {
                    for (var i = 0; i < source.ImplementedInterfaceNames.Count; i++)
                    {
                        context.Writer.Write(source.ImplementedInterfaceNames[i]);

                        if (i + 1 < source.ImplementedInterfaceNames.Count)
                        {
                            context.Writer.WriteParameterSeparator();
                        }
                    }
                }

                context.Writer.WriteLine();

                using (context.Writer.BuildScope())
                {
                    context.Writer
                        .Write("private static object @")
                        .Write(DesignTimeVariable)
                        .WriteLine(";");

                    context.Render(source.Children);
                }
            }

            private void Render(RenderTagHelper source, CSharpRenderingContext context)
            {
                var renderTagHelperContext = new RenderTagHelperContext();
                using (context.UseRenderTagHelperContext(renderTagHelperContext))
                {
                    context.Render(source.Children);
                }
            }

            private void Render(InitializeTagHelperStructure source, CSharpRenderingContext context)
            {
                context.Render(source.Children);
            }

            private void Render(CreateTagHelper source, CSharpRenderingContext context)
            {
                var tagHelperVariableName = GetTagHelperVariableName(source.TagHelperTypeName);

                // Create the tag helper
                context.Writer
                    .WriteStartAssignment(tagHelperVariableName)
                    .WriteStartMethodInvocation(
                        context.CodeLiterals.GeneratedTagHelperContext.CreateTagHelperMethodName,
                        "global::" + source.TagHelperTypeName)
                    .WriteEndMethodInvocation();
            }

            private void Render(SetTagHelperProperty source, CSharpRenderingContext context)
            {
                var tagHelperVariableName = GetTagHelperVariableName(source.TagHelperTypeName);
                var renderTagHelperContext = context.GetRenderTagHelperContext();
                var propertyValueAccessor = GetTagHelperPropertyAccessor(tagHelperVariableName, source.AttributeName, source.AssociatedDescriptor);

                string previousValueAccessor;
                if (renderTagHelperContext.RenderedBoundAttributes.TryGetValue(source.AttributeName, out previousValueAccessor))
                {
                    context.Writer
                        .WriteStartAssignment(propertyValueAccessor)
                        .Write(previousValueAccessor)
                        .WriteLine(";");

                    return;
                }
                else
                {
                    renderTagHelperContext.RenderedBoundAttributes[source.AttributeName] = propertyValueAccessor;
                }

                if (source.AssociatedDescriptor.IsStringProperty)
                {
                    context.Render(source.Value.Children);
                }
                else
                {
                    var firstMappedChild = source.Value.Children.FirstOrDefault(child => child is ISourceMapped) as ISourceMapped;
                    var valueStart = firstMappedChild?.DocumentLocation;

                    using (context.Writer.BuildLinePragma(source.DocumentLocation))
                    using (context.Writer.NoIndent())
                    {
                        var assignmentPrefixLength = propertyValueAccessor.Length + " = ".Length;
                        if (source.AssociatedDescriptor.IsEnum &&
                            source.Value.Children.Count == 1 &&
                            source.Value.Children.First() is RenderHtml)
                        {
                            assignmentPrefixLength += $"global::{source.AssociatedDescriptor.TypeName}.".Length;

                            if (valueStart != null)
                            {
                                var padding = Math.Max(valueStart.CharacterIndex - assignmentPrefixLength, 0);
                                var paddingString = _paddingBuilder.BuildPaddingString(padding);

                                context.Writer.Write(paddingString);
                            }

                            context.Writer
                                .WriteStartAssignment(propertyValueAccessor)
                                .Write("global::")
                                .Write(source.AssociatedDescriptor.TypeName)
                                .Write(".");
                        }
                        else
                        {
                            if (valueStart != null)
                            {
                                var padding = Math.Max(valueStart.CharacterIndex - assignmentPrefixLength, 0);
                                var paddingString = _paddingBuilder.BuildPaddingString(padding);

                                context.Writer.Write(paddingString);
                            }

                            context.Writer.WriteStartAssignment(propertyValueAccessor);
                        }

                        RenderTagHelperAttributeInline(source.Value, source.DocumentLocation, context);

                        context.Writer.WriteLine(";");
                    }
                }
            }

            private void Render(DeclareTagHelperFields source, CSharpRenderingContext context)
            {
                foreach (var tagHelperTypeName in source.UsedTagHelperTypeNames)
                {
                    var tagHelperVariableName = GetTagHelperVariableName(tagHelperTypeName);
                    context.Writer
                        .Write("private global::")
                        .WriteVariableDeclaration(
                            tagHelperTypeName,
                            tagHelperVariableName,
                            value: null);
                }
            }

            private string BuildAssignmentOffsetPadding(int padding)
            {
                var offsetPadding = Math.Max(padding - DesignTimeVariable.Length - " = ".Length, 0);
                var paddingString = _paddingBuilder.BuildPaddingString(offsetPadding);

                return paddingString;
            }

            private void RenderTagHelperAttributeInline(
                ICSharpSource attributeValue,
                MappingLocation documentLocation,
                CSharpRenderingContext context)
            {
                if (attributeValue is CSharpSource)
                {
                    var source = (CSharpSource)attributeValue;
                    if (source.DocumentLocation != null)
                    {
                        using (context.Writer.BuildCodeMapping(source.DocumentLocation))
                        {
                            context.Writer.Write(source.Code);
                        }
                    }
                    else
                    {
                        context.Writer.Write(source.Code);
                    }
                }
                else if (attributeValue is RenderHtml)
                {
                    var source = (RenderHtml)attributeValue;
                    if (source.DocumentLocation != null)
                    {
                        using (context.Writer.BuildCodeMapping(source.DocumentLocation))
                        {
                            context.Writer.Write(source.Html);
                        }
                    }
                    else
                    {
                        context.Writer.Write(source.Html);
                    }
                }
                else if (attributeValue is RenderExpression)
                {
                    var source = (RenderExpression)attributeValue;
                    using (context.Writer.BuildCodeMapping(source.DocumentLocation))
                    {
                        RenderTagHelperAttributeInline(((RenderExpression)attributeValue).Expression, documentLocation, context);
                    }
                }
                else if (attributeValue is RenderStatement)
                {
                    context.ErrorSink.OnError(
                        documentLocation,
                        "TODO: RazorResources.TagHelpers_CodeBlocks_NotSupported_InAttributes");
                }
                else if (attributeValue is Template)
                {
                    context.ErrorSink.OnError(
                        documentLocation,
                        "TODO: RazorResources.FormatTagHelpers_InlineMarkupBlocks_NotSupported_InAttributes(_attributeTypeName)");
                }
                else if (attributeValue is CSharpBlock)
                {
                    var expressionBlock = (CSharpBlock)attributeValue;
                    for (var i = 0; i < expressionBlock.Children.Count; i++)
                    {
                        RenderTagHelperAttributeInline(expressionBlock.Children[i], documentLocation, context);
                    }
                }
            }

            private static string GetTagHelperPropertyAccessor(
                string tagHelperVariableName,
                string attributeName,
                TagHelperAttributeDescriptor associatedDescriptor)
            {
                var propertyAccessor = $"{tagHelperVariableName}.{associatedDescriptor.PropertyName}";

                if (associatedDescriptor.IsIndexer)
                {
                    var dictionaryKey = attributeName.Substring(associatedDescriptor.Name.Length);
                    propertyAccessor += $"[\"{dictionaryKey}\"]";
                }

                return propertyAccessor;
            }

            private static string GetTagHelperVariableName(string tagHelperTypeName) => "__" + tagHelperTypeName.Replace('.', '_');
        }

        public class PageStructureCSharpRenderer : ICSharpRenderer
        {
            public RazorEngine Engine { get; set; }

            public int Order { get; } = 2;

            public bool TryRender(ICSharpSource source, CSharpRenderingContext context)
            {
                if (source is NamespaceDeclaration)
                {
                    Render((NamespaceDeclaration)source, context);
                }
                else if (source is ExecuteMethodDeclaration)
                {
                    Render((ExecuteMethodDeclaration)source, context);
                }
                else if (source is ViewClassDeclaration)
                {
                    Render((ViewClassDeclaration)source, context);
                }
                else
                {
                    return false;
                }

                return true;
            }

            private void Render(NamespaceDeclaration source, CSharpRenderingContext context)
            {
                context.Writer
                    .Write("namespace ")
                    .WriteLine(source.Namespace);

                using (context.Writer.BuildScope())
                {
                    context.Writer.WriteLineHiddenDirective();
                    context.Render(source.Children);
                }
            }

            private void Render(ExecuteMethodDeclaration source, CSharpRenderingContext context)
            {
                context.Writer
                    .WriteLine("#pragma warning disable 1998")
                    .Write(source.Accessor)
                    .Write(" ");

                for (var i = 0; i < source.Modifiers.Count; i++)
                {
                    context.Writer.Write(source.Modifiers[i]);

                    if (i + 1 < source.Modifiers.Count)
                    {
                        context.Writer.Write(" ");
                    }
                }

                context.Writer
                    .Write(" ")
                    .Write(source.ReturnTypeName)
                    .Write(" ")
                    .Write(source.Name)
                    .WriteLine("()");

                using (context.Writer.BuildScope())
                {
                    context.Render(source.Children);
                }

                context.Writer.WriteLine("#pragma warning restore 1998");
            }

            private void Render(ViewClassDeclaration source, CSharpRenderingContext context)
            {
                context.Writer
                    .Write(source.Accessor)
                    .Write(" class ")
                    .Write(source.Name);

                if (source.BaseTypeName != null || source.ImplementedInterfaceNames != null)
                {
                    context.Writer.Write(" : ");
                }

                if (source.BaseTypeName != null)
                {
                    context.Writer.Write(source.BaseTypeName);

                    if (source.ImplementedInterfaceNames != null)
                    {
                        context.Writer.WriteParameterSeparator();
                    }
                }

                if (source.ImplementedInterfaceNames != null)
                {
                    for (var i = 0; i < source.ImplementedInterfaceNames.Count; i++)
                    {
                        context.Writer.Write(source.ImplementedInterfaceNames[i]);

                        if (i + 1 < source.ImplementedInterfaceNames.Count)
                        {
                            context.Writer.WriteParameterSeparator();
                        }
                    }
                }

                context.Writer.WriteLine();

                using (context.Writer.BuildScope())
                {
                    context.Render(source.Children);
                }
            }
        }

        public class RuntimeCSharpRenderer : ICSharpRenderer
        {
            private const string ExecutionContextVariableName = "__tagHelperExecutionContext";
            private const string StringValueBufferVariableName = "__tagHelperStringValueBuffer";
            private const string ScopeManagerVariableName = "__tagHelperScopeManager";
            private const string RunnerVariableName = "__tagHelperRunner";

            public RazorEngine Engine { get; set; }

            public int Order { get; }

            public bool TryRender(RazorIRNode node, CSharpRenderingContext context)
            {
                if (node is Checksum)
                {
                    Render((Checksum)node, context);
                }
                else if (node is CSharpSource)
                {
                    Render((CSharpSource)node, context);
                }
                else if (node is RenderHtml)
                {
                    Render((RenderHtml)node, context);
                }
                else if (node is RenderExpression)
                {
                    Render((RenderExpression)node, context);
                }
                else if (node is RenderTagHelper)
                {
                    Render((RenderTagHelper)node, context);
                }
                else if (node is InitializeTagHelperStructure)
                {
                    Render((InitializeTagHelperStructure)node, context);
                }
                else if (node is CreateTagHelper)
                {
                    Render((CreateTagHelper)node, context);
                }
                else if (node is AddPreallocatedTagHelperHtmlAttribute)
                {
                    Render((AddPreallocatedTagHelperHtmlAttribute)node, context);
                }
                else if (node is AddTagHelperHtmlAttribute)
                {
                    Render((AddTagHelperHtmlAttribute)node, context);
                }
                else if (node is SetPreallocatedTagHelperProperty)
                {
                    Render((SetPreallocatedTagHelperProperty)node, context);
                }
                else if (node is SetTagHelperProperty)
                {
                    Render((SetTagHelperProperty)node, context);
                }
                else if (node is ExecuteTagHelpers)
                {
                    Render((ExecuteTagHelpers)node, context);
                }
                else if (node is DeclarePreallocatedTagHelperHtmlAttribute)
                {
                    Render((DeclarePreallocatedTagHelperHtmlAttribute)node, context);
                }
                else if (node is DeclarePreallocatedTagHelperAttribute)
                {
                    Render((DeclarePreallocatedTagHelperAttribute)node, context);
                }
                else if (node is BeginInstrumentation)
                {
                    Render((BeginInstrumentation)node, context);
                }
                else if (node is EndInstrumentation)
                {
                    Render((EndInstrumentation)node, context);
                }
                else if (node is ImportNamespace)
                {
                    Render((ImportNamespace)node, context);
                }
                else if (node is RenderConditionalAttribute)
                {
                    Render((RenderConditionalAttribute)node, context);
                }
                else if (node is LiteralAttributePiece)
                {
                    Render((LiteralAttributePiece)node, context);
                }
                else if (node is ConditionalAttributePiece)
                {
                    Render((ConditionalAttributePiece)node, context);
                }
                else if (node is RenderSection)
                {
                    Render((RenderSection)node, context);
                }
                else if (node is RenderStatement)
                {
                    Render((RenderStatement)node, context);
                }
                else if (node is Template)
                {
                    Render((Template)node, context);
                }
                else if (node is DeclareTagHelperFields)
                {
                    Render((DeclareTagHelperFields)node, context);
                }
                else
                {
                    return false;
                }

                return true;
            }

            private static void Render(Checksum source, CSharpRenderingContext context)
            {
                if (!string.IsNullOrEmpty(source.Bytes))
                {
                    context.Writer
                    .Write("#pragma checksum \"")
                    .Write(source.FileName)
                    .Write("\" \"")
                    .Write(source.Guid)
                    .Write("\" \"")
                    .Write(source.Bytes)
                    .WriteLine("\"");
                }
            }

            private static void Render(CSharpTokenIRNode source, CSharpRenderingContext context)
            {
                context.Writer.Write(source.Code);
            }

            private static void Render(RenderHtml source, CSharpRenderingContext context)
            {
                const int MaxStringLiteralLength = 1024;

                var charactersConsumed = 0;
                var renderingConventions = context.GetRenderingConventions();

                // Render the string in pieces to avoid Roslyn OOM exceptions at compile time: https://github.com/aspnet/External/issues/54
                while (charactersConsumed < source.Html.Length)
                {
                    string textToRender;
                    if (source.Html.Length <= MaxStringLiteralLength)
                    {
                        textToRender = source.Html;
                    }
                    else
                    {
                        var charactersToSubstring = Math.Min(MaxStringLiteralLength, source.Html.Length - charactersConsumed);
                        textToRender = source.Html.Substring(charactersConsumed, charactersToSubstring);
                    }

                    renderingConventions
                        .StartWriteLiteralMethod()
                        .WriteStringLiteral(textToRender)
                        .WriteEndMethodInvocation();

                    charactersConsumed += textToRender.Length;
                }
            }

            private static void Render(RenderExpression source, CSharpRenderingContext context)
            {
                IDisposable linePragmaScope = null;
                if (source.DocumentLocation != null)
                {
                    linePragmaScope = context.Writer.BuildLinePragma(source.DocumentLocation);
                }

                var renderingConventions = context.GetRenderingConventions();
                renderingConventions.StartWriteMethod();
                context.Render(source.Expression.Children);
                context.Writer.WriteEndMethodInvocation();

                linePragmaScope?.Dispose();
            }

            private static void Render(BeginInstrumentation source, CSharpRenderingContext context)
            {
                context.Writer
                    .WriteStartMethodInvocation(context.CodeLiterals.BeginContextMethodName)
                    .Write(source.DocumentLocation.AbsoluteIndex.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .Write(source.DocumentLocation.ContentLength.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .Write(source.Literal ? "true" : "false")
                    .WriteEndMethodInvocation();
            }

            private static void Render(EndInstrumentation source, CSharpRenderingContext context)
            {
                context.Writer.WriteMethodInvocation(context.CodeLiterals.EndContextMethodName);
            }

            private static void Render(ImportNamespace source, CSharpRenderingContext context)
            {
                context.Writer.WriteUsing(source.Namespace);
            }

            private static void Render(RenderConditionalAttribute source, CSharpRenderingContext context)
            {
                var valuePieceCount = source.ValuePieces.Count(piece => piece is LiteralAttributePiece || piece is ConditionalAttributePiece);
                var prefixLocation = source.DocumentLocation.AbsoluteIndex;
                var suffixLocation = source.DocumentLocation.AbsoluteIndex + source.DocumentLocation.ContentLength - source.Suffix.Length;
                var renderingConventions = context.GetRenderingConventions();
                renderingConventions
                    .StartBeginWriteAttributeMethod()
                    .WriteStringLiteral(source.Name)
                    .WriteParameterSeparator()
                    .WriteStringLiteral(source.Prefix)
                    .WriteParameterSeparator()
                    .Write(prefixLocation.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .WriteStringLiteral(source.Suffix)
                    .WriteParameterSeparator()
                    .Write(suffixLocation.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .Write(valuePieceCount.ToString(CultureInfo.InvariantCulture))
                    .WriteEndMethodInvocation();

                context.Render(source.ValuePieces);

                renderingConventions
                    .StartEndWriteAttributeMethod()
                    .WriteEndMethodInvocation();
            }

            private static void Render(LiteralAttributePiece source, CSharpRenderingContext context)
            {
                var prefixLocation = source.DocumentLocation.AbsoluteIndex;
                var valueLocation = source.DocumentLocation.AbsoluteIndex + source.Prefix.Length;
                var valueLength = source.DocumentLocation.ContentLength - source.Prefix.Length;
                var renderingConventions = context.GetRenderingConventions();
                renderingConventions
                    .StartWriteAttributeValueMethod()
                    .WriteStringLiteral(source.Prefix)
                    .WriteParameterSeparator()
                    .Write(prefixLocation.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .WriteStringLiteral(source.Value)
                    .WriteParameterSeparator()
                    .Write(valueLocation.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .Write(valueLength.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .WriteBooleanLiteral(true)
                    .WriteEndMethodInvocation();
            }

            private static void Render(ConditionalAttributePiece source, CSharpRenderingContext context)
            {
                const string ValueWriterName = "__razor_attribute_value_writer";

                var expressionValue = source.Value.Children.First() as RenderExpression;
                var linePragma = expressionValue != null ? context.Writer.BuildLinePragma(source.DocumentLocation) : null;
                var prefixLocation = source.DocumentLocation.AbsoluteIndex;
                var valueLocation = source.DocumentLocation.AbsoluteIndex + source.Prefix.Length;
                var valueLength = source.DocumentLocation.ContentLength - source.Prefix.Length;
                var renderingConventions = context.GetRenderingConventions();
                renderingConventions
                    .StartWriteAttributeValueMethod()
                    .WriteStringLiteral(source.Prefix)
                    .WriteParameterSeparator()
                    .Write(prefixLocation.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator();

                if (expressionValue != null)
                {
                    Debug.Assert(source.Value.Children.Count == 1);

                    RenderExpressionInline(expressionValue.Expression, context);
                }
                else
                {
                    // Not an expression; need to buffer the result.
                    context.Writer.WriteStartNewObject(context.CodeLiterals.TemplateTypeName);

                    var redirectConventions = new CSharpRedirectRenderingConventions(ValueWriterName, context);
                    using (context.UseRenderingConventions(redirectConventions))
                    using (context.Writer.BuildAsyncLambda(endLine: false, parameterNames: ValueWriterName))
                    {
                        context.Render(source.Value.Children);
                    }

                    context.Writer.WriteEndMethodInvocation(false);
                }

                context.Writer
                    .WriteParameterSeparator()
                    .Write(valueLocation.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .Write(valueLength.ToString(CultureInfo.InvariantCulture))
                    .WriteParameterSeparator()
                    .WriteBooleanLiteral(false)
                    .WriteEndMethodInvocation();

                linePragma?.Dispose();
            }

            private static void Render(RenderSection source, CSharpRenderingContext context)
            {
                const string SectionWriterName = "__razor_section_writer";

                context.Writer
                    .WriteStartMethodInvocation(context.CodeLiterals.DefineSectionMethodName)
                    .WriteStringLiteral(source.Name)
                    .WriteParameterSeparator();

                var redirectConventions = new CSharpRedirectRenderingConventions(SectionWriterName, context);
                using (context.UseRenderingConventions(redirectConventions))
                using (context.Writer.BuildAsyncLambda(endLine: false, parameterNames: SectionWriterName))
                {
                    context.Render(source.Children);
                }

                context.Writer.WriteEndMethodInvocation();
            }

            private static void Render(RenderStatement source, CSharpRenderingContext context)
            {
                if (string.IsNullOrWhiteSpace(source.Code))
                {
                    return;
                }

                if (source.DocumentLocation != null)
                {
                    using (context.Writer.BuildLinePragma(source.DocumentLocation))
                    {
                        context.Writer.WriteLine(source.Code);
                    }
                }
                else
                {
                    context.Writer.WriteLine(source.Code);
                }
            }

            private static void Render(Template source, CSharpRenderingContext context)
            {
                const string ItemParameterName = "item";
                const string TemplateWriterName = "__razor_template_writer";

                context.Writer
                    .Write(ItemParameterName).Write(" => ")
                    .WriteStartNewObject(context.CodeLiterals.TemplateTypeName);

                var redirectConventions = new CSharpRedirectRenderingConventions(TemplateWriterName, context);
                using (context.UseRenderingConventions(redirectConventions))
                using (context.Writer.BuildAsyncLambda(endLine: false, parameterNames: TemplateWriterName))
                {
                    context.Render(source.Children);
                }

                context.Writer.WriteEndMethodInvocation();
            }

            private static void Render(DeclarePreallocatedTagHelperHtmlAttribute source, CSharpRenderingContext context)
            {
                context.Writer
                    .Write("private static readonly global::")
                    .Write(context.CodeLiterals.GeneratedTagHelperContext.TagHelperAttributeTypeName)
                    .Write(" ")
                    .Write(source.VariableName)
                    .Write(" = ")
                    .WriteStartNewObject("global::" + context.CodeLiterals.GeneratedTagHelperContext.TagHelperAttributeTypeName)
                    .WriteStringLiteral(source.Name);

                if (source.ValueStyle == HtmlAttributeValueStyle.Minimized)
                {
                    context.Writer.WriteEndMethodInvocation();
                }
                else
                {
                    context.Writer
                        .WriteParameterSeparator()
                        .WriteStartNewObject("global::" + context.CodeLiterals.GeneratedTagHelperContext.EncodedHtmlStringTypeName)
                        .WriteStringLiteral(source.Value)
                        .WriteEndMethodInvocation(endLine: false)
                        .WriteParameterSeparator()
                        .Write($"global::{typeof(HtmlAttributeValueStyle).FullName}.{source.ValueStyle}")
                        .WriteEndMethodInvocation();
                }
            }

            private static void Render(DeclarePreallocatedTagHelperAttribute source, CSharpRenderingContext context)
            {
                context.Writer
                    .Write("private static readonly global::")
                    .Write(context.CodeLiterals.GeneratedTagHelperContext.TagHelperAttributeTypeName)
                    .Write(" ")
                    .Write(source.VariableName)
                    .Write(" = ")
                    .WriteStartNewObject("global::" + context.CodeLiterals.GeneratedTagHelperContext.TagHelperAttributeTypeName)
                    .WriteStringLiteral(source.Name)
                    .WriteParameterSeparator()
                    .WriteStringLiteral(source.Value)
                    .WriteParameterSeparator()
                    .Write($"global::{typeof(HtmlAttributeValueStyle).FullName}.{source.ValueStyle}")
                    .WriteEndMethodInvocation();
            }

            private static void Render(DeclareTagHelperFields source, CSharpRenderingContext context)
            {
                var tagHelperCodeLiterals = context.CodeLiterals.GeneratedTagHelperContext;
                context.Writer.WriteLineHiddenDirective();

                // Need to disable the warning "X is assigned to but never used." for the value buffer since
                // whether it's used depends on how a TagHelper is used.
                context.Writer
                    .WritePragma("warning disable 0414")
                    .Write("private ")
                    .WriteVariableDeclaration("string", StringValueBufferVariableName, value: null)
                    .WritePragma("warning restore 0414");

                context.Writer
                    .Write("private global::")
                    .WriteVariableDeclaration(
                        tagHelperCodeLiterals.ExecutionContextTypeName,
                        ExecutionContextVariableName,
                        value: null);

                context.Writer
                    .Write("private global::")
                    .Write(tagHelperCodeLiterals.RunnerTypeName)
                    .Write(" ")
                    .Write(RunnerVariableName)
                    .Write(" = new global::")
                    .Write(tagHelperCodeLiterals.RunnerTypeName)
                    .WriteLine("();");

                const string backedScopeManageVariableName = "__backed" + ScopeManagerVariableName;
                context.Writer
                    .Write("private global::")
                    .WriteVariableDeclaration(
                        tagHelperCodeLiterals.ScopeManagerTypeName,
                        backedScopeManageVariableName,
                        value: null);

                context.Writer
                    .Write("private global::")
                    .Write(tagHelperCodeLiterals.ScopeManagerTypeName)
                    .Write(" ")
                    .WriteLine(ScopeManagerVariableName);

                using (context.Writer.BuildScope())
                {
                    context.Writer.WriteLine("get");
                    using (context.Writer.BuildScope())
                    {
                        context.Writer
                            .Write("if (")
                            .Write(backedScopeManageVariableName)
                            .WriteLine(" == null)");

                        using (context.Writer.BuildScope())
                        {
                            context.Writer
                                .WriteStartAssignment(backedScopeManageVariableName)
                                .WriteStartNewObject(tagHelperCodeLiterals.ScopeManagerTypeName)
                                .Write(tagHelperCodeLiterals.StartTagHelperWritingScopeMethodName)
                                .WriteParameterSeparator()
                                .Write(tagHelperCodeLiterals.EndTagHelperWritingScopeMethodName)
                                .WriteEndMethodInvocation();
                        }

                        context.Writer.WriteReturn(backedScopeManageVariableName);
                    }
                }

                foreach (var tagHelperTypeName in source.UsedTagHelperTypeNames)
                {
                    var tagHelperVariableName = GetTagHelperVariableName(tagHelperTypeName);
                    context.Writer
                        .Write("private global::")
                        .WriteVariableDeclaration(
                            tagHelperTypeName,
                            tagHelperVariableName,
                            value: null);
                }
            }

            private static void Render(RenderTagHelper source, CSharpRenderingContext context)
            {
                var renderTagHelperContext = new RenderTagHelperContext();
                using (context.UseRenderTagHelperContext(renderTagHelperContext))
                {
                    context.Render(source.Children);
                }
            }

            private void Render(InitializeTagHelperStructure source, CSharpRenderingContext context)
            {
                // Call into the tag helper scope manager to start a new tag helper scope.
                // Also capture the value as the current execution context.
                context.Writer
                    .WriteStartAssignment(ExecutionContextVariableName)
                    .WriteStartInstanceMethodInvocation(
                        ScopeManagerVariableName,
                        context.CodeLiterals.GeneratedTagHelperContext.ScopeManagerBeginMethodName);

                // Assign a unique ID for this instance of the source HTML tag. This must be unique
                // per call site, e.g. if the tag is on the view twice, there should be two IDs.
                context.Writer.WriteStringLiteral(source.TagName)
                       .WriteParameterSeparator()
                       .Write("global::")
                       .Write(typeof(TagMode).FullName)
                       .Write(".")
                       .Write(source.TagMode.ToString())
                       .WriteParameterSeparator()
                       .WriteStringLiteral(GenerateUniqueTagHelperId())
                       .WriteParameterSeparator();

                // We remove and redirect writers so TagHelper authors can retrieve content.
                var nonRedirectedConventions = new CSharpRenderingConventions(context);
                using (context.UseRenderingConventions(nonRedirectedConventions))
                using (context.Writer.BuildAsyncLambda(endLine: false))
                {
                    context.Render(source.Children);
                }

                context.Writer.WriteEndMethodInvocation();
            }

            private static void Render(CreateTagHelper source, CSharpRenderingContext context)
            {
                var tagHelperVariableName = GetTagHelperVariableName(source.TagHelperTypeName);

                // Create the tag helper
                context.Writer
                    .WriteStartAssignment(tagHelperVariableName)
                    .WriteStartMethodInvocation(
                        context.CodeLiterals.GeneratedTagHelperContext.CreateTagHelperMethodName,
                        "global::" + source.TagHelperTypeName)
                    .WriteEndMethodInvocation();

                context.Writer.WriteInstanceMethodInvocation(
                    ExecutionContextVariableName,
                    context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextAddMethodName,
                    tagHelperVariableName);
            }

            private static void Render(AddPreallocatedTagHelperHtmlAttribute source, CSharpRenderingContext context)
            {
                context.Writer
                    .WriteStartInstanceMethodInvocation(
                        ExecutionContextVariableName,
                        context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextAddHtmlAttributeMethodName)
                    .Write(source.AttributeVariableName)
                    .WriteEndMethodInvocation();
            }

            private static void Render(AddTagHelperHtmlAttribute source, CSharpRenderingContext context)
            {
                var attributeValueStyleParameter = $"global::{typeof(HtmlAttributeValueStyle).FullName}.{source.ValueStyle}";
                var isConditionalAttributeValue = source.ValuePieces.Any(child => child is ConditionalAttributePiece);

                // All simple text and minimized attributes will be pre-allocated.
                if (isConditionalAttributeValue)
                {
                    // Dynamic attribute value should be run through the conditional attribute removal system. It's
                    // unbound and contains C#.

                    // TagHelper attribute rendering is buffered by default. We do not want to write to the current
                    // writer.
                    var valuePieceCount = source.ValuePieces.Count(piece => piece is LiteralAttributePiece || piece is ConditionalAttributePiece);

                    context.Writer
                        .WriteStartMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.BeginAddHtmlAttributeValuesMethodName)
                        .Write(ExecutionContextVariableName)
                        .WriteParameterSeparator()
                        .WriteStringLiteral(source.Name)
                        .WriteParameterSeparator()
                        .Write(valuePieceCount.ToString(CultureInfo.InvariantCulture))
                        .WriteParameterSeparator()
                        .Write(attributeValueStyleParameter)
                        .WriteEndMethodInvocation();

                    var renderingConventions = new TagHelperHtmlAttributeRenderingConventions(context);
                    using (context.UseRenderingConventions(renderingConventions))
                    {
                        context.Render(source.ValuePieces);
                    }

                    context.Writer
                        .WriteMethodInvocation(
                            context.CodeLiterals.GeneratedTagHelperContext.EndAddHtmlAttributeValuesMethodName,
                            ExecutionContextVariableName);
                }
                else
                {
                    // This is a data-* attribute which includes C#. Do not perform the conditional attribute removal or
                    // other special cases used when IsDynamicAttributeValue(). But the attribute must still be buffered to
                    // determine its final value.

                    // Attribute value is not plain text, must be buffered to determine its final value.
                    context.Writer.WriteMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.BeginWriteTagHelperAttributeMethodName);

                    // We're building a writing scope around the provided chunks which captures everything written from the
                    // page. Therefore, we do not want to write to any other buffer since we're using the pages buffer to
                    // ensure we capture all content that's written, directly or indirectly.
                    var nonRedirectedConventions = new CSharpRenderingConventions(context);
                    using (context.UseRenderingConventions(nonRedirectedConventions))
                    {
                        context.Render(source.ValuePieces);
                    }

                    context.Writer
                        .WriteStartAssignment(StringValueBufferVariableName)
                        .WriteMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.EndWriteTagHelperAttributeMethodName)
                        .WriteStartInstanceMethodInvocation(
                            ExecutionContextVariableName,
                            context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextAddHtmlAttributeMethodName)
                        .WriteStringLiteral(source.Name)
                        .WriteParameterSeparator()
                        .WriteStartMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.MarkAsHtmlEncodedMethodName)
                        .Write(StringValueBufferVariableName)
                        .WriteEndMethodInvocation(endLine: false)
                        .WriteParameterSeparator()
                        .Write(attributeValueStyleParameter)
                        .WriteEndMethodInvocation();
                }
            }

            private static void Render(SetPreallocatedTagHelperProperty source, CSharpRenderingContext context)
            {
                var tagHelperVariableName = GetTagHelperVariableName(source.TagHelperTypeName);
                var propertyValueAccessor = GetTagHelperPropertyAccessor(tagHelperVariableName, source.AttributeName, source.AssociatedDescriptor);
                var attributeValueAccessor = $"{source.AttributeVariableName}.{context.CodeLiterals.GeneratedTagHelperContext.TagHelperAttributeValuePropertyName}";
                context.Writer
                    .WriteStartAssignment(propertyValueAccessor)
                    .Write("(string)")
                    .Write(attributeValueAccessor)
                    .WriteLine(";")
                    .WriteStartInstanceMethodInvocation(
                        ExecutionContextVariableName,
                        context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextAddTagHelperAttributeMethodName)
                    .Write(source.AttributeVariableName)
                    .WriteEndMethodInvocation();
            }

            private static void Render(SetTagHelperProperty source, CSharpRenderingContext context)
            {
                var tagHelperVariableName = GetTagHelperVariableName(source.TagHelperTypeName);
                var renderTagHelperContext = context.GetRenderTagHelperContext();

                // Ensure that the property we're trying to set has initialized its dictionary bound properties.
                if (source.AssociatedDescriptor.IsIndexer &&
                    renderTagHelperContext.VerifiedPropertyDictionaries.Add(source.AssociatedDescriptor.PropertyName))
                {
                    // Throw a reasonable Exception at runtime if the dictionary property is null.
                    context.Writer
                        .Write("if (")
                        .Write(tagHelperVariableName)
                        .Write(".")
                        .Write(source.AssociatedDescriptor.PropertyName)
                        .WriteLine(" == null)");
                    using (context.Writer.BuildScope())
                    {
                        // System is in Host.NamespaceImports for all MVC scenarios. No need to generate FullName
                        // of InvalidOperationException type.
                        context.Writer
                            .Write("throw ")
                            .WriteStartNewObject(nameof(InvalidOperationException))
                            .WriteStartMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.FormatInvalidIndexerAssignmentMethodName)
                            .WriteStringLiteral(source.AttributeName)
                            .WriteParameterSeparator()
                            .WriteStringLiteral(source.TagHelperTypeName)
                            .WriteParameterSeparator()
                            .WriteStringLiteral(source.AssociatedDescriptor.PropertyName)
                            .WriteEndMethodInvocation(endLine: false)   // End of method call
                            .WriteEndMethodInvocation();   // End of new expression / throw statement
                    }
                }

                var propertyValueAccessor = GetTagHelperPropertyAccessor(tagHelperVariableName, source.AttributeName, source.AssociatedDescriptor);

                string previousValueAccessor;
                if (renderTagHelperContext.RenderedBoundAttributes.TryGetValue(source.AttributeName, out previousValueAccessor))
                {
                    context.Writer
                        .WriteStartAssignment(propertyValueAccessor)
                        .Write(previousValueAccessor)
                        .WriteLine(";");

                    return;
                }
                else
                {
                    renderTagHelperContext.RenderedBoundAttributes[source.AttributeName] = propertyValueAccessor;
                }

                if (source.AssociatedDescriptor.IsStringProperty)
                {
                    context.Writer.WriteMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.BeginWriteTagHelperAttributeMethodName);

                    var renderingConventions = new CSharpLiteralCodeConventions(context);
                    using (context.UseRenderingConventions(renderingConventions))
                    {
                        context.Render(source.Value.Children);
                    }

                    context.Writer
                        .WriteStartAssignment(StringValueBufferVariableName)
                        .WriteMethodInvocation(context.CodeLiterals.GeneratedTagHelperContext.EndWriteTagHelperAttributeMethodName)
                        .WriteStartAssignment(propertyValueAccessor)
                        .Write(StringValueBufferVariableName)
                        .WriteLine(";");
                }
                else
                {
                    using (context.Writer.BuildLinePragma(source.DocumentLocation))
                    {
                        context.Writer.WriteStartAssignment(propertyValueAccessor);

                        if (source.AssociatedDescriptor.IsEnum &&
                            source.Value.Children.Count == 1 &&
                            source.Value.Children.First() is RenderHtml)
                        {
                            context.Writer
                                .Write("global::")
                                .Write(source.AssociatedDescriptor.TypeName)
                                .Write(".");
                        }

                        RenderTagHelperAttributeInline(source.Value, source.DocumentLocation, context);

                        context.Writer.WriteLine(";");
                    }
                }

                // We need to inform the context of the attribute value.
                context.Writer
                    .WriteStartInstanceMethodInvocation(
                        ExecutionContextVariableName,
                        context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextAddTagHelperAttributeMethodName)
                    .WriteStringLiteral(source.AttributeName)
                    .WriteParameterSeparator()
                    .Write(propertyValueAccessor)
                    .WriteParameterSeparator()
                    .Write($"global::{typeof(HtmlAttributeValueStyle).FullName}.{source.ValueStyle}")
                    .WriteEndMethodInvocation();
            }

            private static void Render(ExecuteTagHelpers source, CSharpRenderingContext context)
            {
                context.Writer
                    .Write("await ")
                    .WriteStartInstanceMethodInvocation(RunnerVariableName, context.CodeLiterals.GeneratedTagHelperContext.RunnerRunAsyncMethodName)
                    .Write(ExecutionContextVariableName)
                    .WriteEndMethodInvocation();

                var tagHelperOutputAccessor =
                $"{ExecutionContextVariableName}.{context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextOutputPropertyName}";

                context.Writer
                    .Write("if (!")
                    .Write(tagHelperOutputAccessor)
                    .Write(".")
                    .Write(context.CodeLiterals.GeneratedTagHelperContext.TagHelperOutputIsContentModifiedPropertyName)
                    .WriteLine(")");

                using (context.Writer.BuildScope())
                {
                    context.Writer
                        .Write("await ")
                        .WriteInstanceMethodInvocation(
                            ExecutionContextVariableName,
                            context.CodeLiterals.GeneratedTagHelperContext.ExecutionContextSetOutputContentAsyncMethodName);
                }

                var renderingConventions = context.GetRenderingConventions();
                renderingConventions
                    .StartWriteMethod()
                    .Write(tagHelperOutputAccessor)
                    .WriteEndMethodInvocation()
                    .WriteStartAssignment(ExecutionContextVariableName)
                    .WriteInstanceMethodInvocation(
                        ScopeManagerVariableName,
                        context.CodeLiterals.GeneratedTagHelperContext.ScopeManagerEndMethodName);
            }

            protected virtual string GenerateUniqueTagHelperId() => Guid.NewGuid().ToString("N");

            private static void RenderTagHelperAttributeInline(
                ICSharpSource attributeValue,
                MappingLocation documentLocation,
                CSharpRenderingContext context)
            {
                if (attributeValue is CSharpSource)
                {
                    context.Writer.Write(((CSharpSource)attributeValue).Code);
                }
                else if (attributeValue is RenderHtml)
                {
                    context.Writer.Write(((RenderHtml)attributeValue).Html);
                }
                else if (attributeValue is RenderExpression)
                {
                    RenderTagHelperAttributeInline(((RenderExpression)attributeValue).Expression, documentLocation, context);
                }
                else if (attributeValue is RenderStatement)
                {
                    context.ErrorSink.OnError(
                        documentLocation,
                        "TODO: RazorResources.TagHelpers_CodeBlocks_NotSupported_InAttributes");
                }
                else if (attributeValue is Template)
                {
                    context.ErrorSink.OnError(
                        documentLocation,
                        "TODO: RazorResources.FormatTagHelpers_InlineMarkupBlocks_NotSupported_InAttributes(_attributeTypeName)");
                }
                else if (attributeValue is CSharpBlock)
                {
                    var expressionBlock = (CSharpBlock)attributeValue;
                    for (var i = 0; i < expressionBlock.Children.Count; i++)
                    {
                        RenderTagHelperAttributeInline(expressionBlock.Children[i], documentLocation, context);
                    }
                }
            }

            private static string GetTagHelperPropertyAccessor(
                string tagHelperVariableName,
                string attributeName,
                TagHelperAttributeDescriptor associatedDescriptor)
            {
                var propertyAccessor = $"{tagHelperVariableName}.{associatedDescriptor.PropertyName}";

                if (associatedDescriptor.IsIndexer)
                {
                    var dictionaryKey = attributeName.Substring(associatedDescriptor.Name.Length);
                    propertyAccessor += $"[\"{dictionaryKey}\"]";
                }

                return propertyAccessor;
            }

            private static string GetTagHelperVariableName(string tagHelperTypeName) => "__" + tagHelperTypeName.Replace('.', '_');

            private class CSharpLiteralCodeConventions : CSharpRenderingConventions
            {
                public CSharpLiteralCodeConventions(CSharpRenderingContext context) : base(context)
                {
                }

                public override CSharpCodeWriter StartWriteMethod() => Writer.WriteStartMethodInvocation(CodeLiterals.WriteLiteralMethodName);
            }

            private class TagHelperHtmlAttributeRenderingConventions : CSharpRenderingConventions
            {
                public TagHelperHtmlAttributeRenderingConventions(CSharpRenderingContext context) : base(context)
                {
                }

                public override CSharpCodeWriter StartWriteAttributeValueMethod() =>
                    Writer.WriteStartMethodInvocation(CodeLiterals.GeneratedTagHelperContext.AddHtmlAttributeValueMethodName);
            }

            private static void RenderExpressionInline(ICSharpSource expression, CSharpRenderingContext context)
            {
                if (expression is CSharpSource)
                {
                    context.Writer.Write(((CSharpSource)expression).Code);
                }
                else if (expression is RenderExpression)
                {
                    RenderExpressionInline(((RenderExpression)expression).Expression, context);
                }
                else if (expression is CSharpBlock)
                {
                    var expressionBlock = (CSharpBlock)expression;
                    for (var i = 0; i < expressionBlock.Children.Count; i++)
                    {
                        RenderExpressionInline(expressionBlock.Children[i], context);
                    }
                }
            }
        }

        public interface ICSharpRenderer : IRazorEngineFeature
        {
            int Order { get; }

            bool TryRender(RazorIRNode node, CSharpRenderingContext context);
        }

        private class CSharpRendererOrchestrator
        {
            private readonly CSharpRenderingContext _generationContext;
            private readonly IList<ICSharpRenderer> _renderers;

            public CSharpRendererOrchestrator(IEnumerable<ICSharpRenderer> renderers)
            {
                _renderers = renderers.OrderBy(renderer => renderer.Order).ToList();
                _generationContext = new CSharpRenderingContext
                {
                    Writer = new CSharpCodeWriter(),
                    Render = Render,
                };
            }

            public RazorCSharpDocument Render(DocumentIRNode irDocument)
            {
                Render(irDocument.Children);

                var generatedCode = _generationContext.Writer.GenerateCode();
                var lineMappings = _generationContext.Writer.LineMappingManager.Mappings;
                var generatedCSharpDocument = new RazorCSharpDocument()
                {
                    GeneratedCode = generatedCode,
                    LineMappings = lineMappings,
                };

                return generatedCSharpDocument;
            }

            private void Render(IList<RazorIRNode> node)
            {
                for (var i = 0; i < node.Count; i++)
                {
                    Render(node[i]);
                }
            }

            private void Render(RazorIRNode node)
            {
                for (var i = 0; i < _renderers.Count; i++)
                {
                    if (_renderers[i].TryRender(node, _generationContext))
                    {
                        // Successfully rendered the source
                        return;
                    }
                }
            }
        }
    }
}
