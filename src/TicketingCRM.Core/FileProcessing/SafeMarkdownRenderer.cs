using Ganss.Xss;
using Markdig;
using TicketingCRM.Domain.Support.Interfaces;
namespace TicketingCRM.Core.FileProcessing;
public sealed class SafeMarkdownRenderer:IRichTextRenderer
{
 private static readonly MarkdownPipeline Pipeline=new MarkdownPipelineBuilder().DisableHtml().Build();
 public string Render(string text)
 {
  var sanitizer=new HtmlSanitizer();sanitizer.AllowedTags.Clear();foreach(var tag in new[]{"p","br","strong","em","ul","ol","li","blockquote","pre","code","a"})sanitizer.AllowedTags.Add(tag);
  sanitizer.AllowedAttributes.Clear();sanitizer.AllowedAttributes.Add("href");sanitizer.AllowedAttributes.Add("title");sanitizer.AllowedCssProperties.Clear();sanitizer.AllowedSchemes.Clear();foreach(var scheme in new[]{"https","http","mailto"})sanitizer.AllowedSchemes.Add(scheme);
  return sanitizer.Sanitize(Markdown.ToHtml((text??"")[..Math.Min(text?.Length??0,8000)],Pipeline));
 }
}
