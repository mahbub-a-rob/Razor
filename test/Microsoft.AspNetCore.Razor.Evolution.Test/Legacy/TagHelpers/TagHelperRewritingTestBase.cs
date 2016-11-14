// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Evolution.Legacy
{
    public class TagHelperRewritingTestBase : CsHtmlMarkupParserTestBase
    {
        internal void RunParseTreeRewriterTest(
            string documentContent,
            MarkupBlock expectedOutput,
            params string[] tagNames)
        {
            RunParseTreeRewriterTest(
                documentContent,
                expectedOutput,
                errors: Enumerable.Empty<RazorError>(),
                tagNames: tagNames);
        }

        internal void RunParseTreeRewriterTest(
            string documentContent,
            MarkupBlock expectedOutput,
            IEnumerable<RazorError> errors,
            params string[] tagNames)
        {
            var providerContext = BuildProviderContext(tagNames);

            EvaluateData(providerContext, documentContent, expectedOutput, errors);
        }

        internal TagHelperDescriptorProvider BuildProviderContext(params string[] tagNames)
        {
            var descriptors = new List<TagHelperDescriptor>();

            foreach (var tagName in tagNames)
            {
                descriptors.Add(
                    new TagHelperDescriptor
                    {
                        TagName = tagName,
                        TypeName = tagName + "taghelper",
                        AssemblyName = "SomeAssembly"
                    });
            }

            return new TagHelperDescriptorProvider(descriptors);
        }

        internal void EvaluateData(
            TagHelperDescriptorProvider provider,
            string documentContent,
            MarkupBlock expectedOutput,
            IEnumerable<RazorError> expectedErrors)
        {
            var razorEngine = RazorEngine.Create(builder =>
            {
                for (var i = builder.Phases.Count - 1; i > 1; i--)
                {
                    builder.Phases.RemoveAt(i);
                }
            });
            var results = ParseDocument(documentContent);
            var actualErrors = results.Diagnostics
                .OrderBy(error => error.Location.AbsoluteIndex)
                .ToList();

            EvaluateRazorErrors(actualErrors, expectedErrors.ToList());
            EvaluateParseTree(results.Root, expectedOutput);
        }
    }
}
