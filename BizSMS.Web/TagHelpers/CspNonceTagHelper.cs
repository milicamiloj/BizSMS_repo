using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BizSMS.Web.TagHelpers
{
    // Dodaje nonce atribut na svaki <script> tag iz vrednosti koju middleware u Program.cs
    // stavi u HttpContext.Items["csp-nonce"]. Time CSP script-src može da izbaci 'unsafe-inline'
    // (A03/A05): izvršavaju se samo skripte sa tačnim nonce-om za taj zahtev, pa ubačena inline
    // skripta ne radi. Pokriva i inline i eksterne <script> tagove (za eksterne je bezopasno).
    [HtmlTargetElement("script")]
    public class CspNonceTagHelper : TagHelper
    {
        public const string NonceItemKey = "csp-nonce";

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; } = default!;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (ViewContext.HttpContext.Items[NonceItemKey] is string nonce && nonce.Length > 0)
                output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}
