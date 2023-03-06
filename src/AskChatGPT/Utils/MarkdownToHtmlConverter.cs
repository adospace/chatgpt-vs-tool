using Markdig.Syntax;
using Markdig;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Markdig.Renderers;
using System.Text.RegularExpressions;

namespace AskChatGPT.Utils;

class MarkdownToHtmlConverter
{
    [ThreadStatic]
    static StringWriter _htmlWriterStatic;

    readonly static MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePragmaLines()
        .UsePreciseSourceLocation()
        .UseYamlFrontMatter()
        .UseEmojiAndSmiley()
        .Build();

    public MarkdownToHtmlConverter()
    {
    }

    public async Task<string> ConvertToHtml(string markdown)
    {
        var htmlWriter = (_htmlWriterStatic ??= new StringWriter());
        htmlWriter.GetStringBuilder().Clear();

        try
        {
            var markdownDocument = Markdown.Parse(markdown, _markdownPipeline);

            HtmlRenderer htmlRenderer = new(htmlWriter);
            //Document.Pipeline.Setup(htmlRenderer);
            htmlRenderer.UseNonAsciiNoEscape = true;
            htmlRenderer.Render(markdownDocument);

            await htmlWriter.FlushAsync();
            string html = htmlWriter.ToString();
            html = Regex.Replace(html, "\"language-(c|C)#\"", "\"language-csharp\"", RegexOptions.Compiled);
            return html;
        }
        catch (Exception ex)
        {
            // We could output this to the exception pane of VS?
            // Though, it's easier to output it directly to the browser
            return "<p>An unexpected exception occurred:</p><pre>" +
                    ex.ToString().Replace("<", "&lt;").Replace("&", "&amp;") + "</pre>";
        }
        finally
        {
            // Free any resources allocated by HtmlWriter
            htmlWriter?.GetStringBuilder().Clear();
        }
    }
}

