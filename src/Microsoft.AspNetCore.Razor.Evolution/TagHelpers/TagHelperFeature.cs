using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Evolution.Legacy;

namespace Microsoft.AspNetCore.Razor.Evolution.TagHelpers
{
    internal class TagHelperFeature : IRazorEngineFeature
    {
        public TagHelperFeature(ITagHelperDescriptorResolver resolver)
        {
            Resolver = resolver;
        }

        public RazorEngine Engine { get; set; }

        public ITagHelperDescriptorResolver Resolver { get; }
    }
}
