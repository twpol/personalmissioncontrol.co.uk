using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;

namespace app
{
    public class LowerCaseUrls : IOutboundParameterTransformer
    {
        public string? TransformOutbound(object value)
        {
            if (value == null)
            {
                return null;
            }

            return Regex.Replace(
                value.ToString(),
                "([a-z])([A-Z])",
                "$1-$2",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)
            ).ToLowerInvariant();
        }
    }
}
