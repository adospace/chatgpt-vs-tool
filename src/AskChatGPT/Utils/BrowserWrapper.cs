using EnvDTE;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using AskChatGPT.Options;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Controls;

namespace AskChatGPT.Utils;

public class BrowserWrapper : IDisposable
{
    private double _cachedPosition = 0,
                   _cachedHeight = 0,
                   _positionPercentage = 0;

    private const string _mappedMarkdownEditorVirtualHostName = "markdown-editor-host";
    private const string _mappedBrowsingFileVirtualHostName = "browsing-file-host";
    private readonly Action<string> _copyCodeAction;

    public WebView2 WebView { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0), Visibility = Visibility.Hidden };
    public bool IsInitialized { get; private set; }

    public event EventHandler Initialized;

    public BrowserWrapper(Action<string> copyCodeAction)
    {
        WebView.Initialized += BrowserInitialized;
        WebView.NavigationStarting += BrowserNavigationStarting;

        WebView.SetResourceReference(Control.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
        _copyCodeAction = copyCodeAction;
    }

    public void Dispose()
    {
        WebView.Initialized -= BrowserInitialized;
        WebView.NavigationStarting -= BrowserNavigationStarting;
        WebView.Dispose();
    }

    private void BrowserInitialized(object sender, EventArgs e)
    {
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await InitializeWebView2CoreAsync();
            SetVirtualFolderMapping();
            WebView.Visibility = Visibility.Visible;

            string offsetHeightResult = await WebView.ExecuteScriptAsync("document.body.offsetHeight;");
            double.TryParse(offsetHeightResult, out _cachedHeight);

            await WebView.ExecuteScriptAsync($@"document.documentElement.scrollTop={_positionPercentage * _cachedHeight / 100}");

            await AdjustAnchorsAsync();

            await UpdateBrowserAsync(string.Empty);

            IsInitialized = true;

            Initialized?.Invoke(this, EventArgs.Empty);
        }).FireAndForget();

        async Task InitializeWebView2CoreAsync()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name);
            CoreWebView2Environment webView2Environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: tempDir, options: null);

            await WebView.EnsureCoreWebView2Async(webView2Environment);
        }

        void SetVirtualFolderMapping()
        {
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(_mappedMarkdownEditorVirtualHostName, GetFolder(), CoreWebView2HostResourceAccessKind.Allow);

            //string baseHref = Path.GetDirectoryName(_file).Replace("\\", "/");
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(_mappedBrowsingFileVirtualHostName, GetFolder(), CoreWebView2HostResourceAccessKind.Allow);
        }
    }

    private void BrowserNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            if (e.Uri == null)
            {
                return;
            }

            // Setting content rather than URL navigating
            if (e.Uri.StartsWith("data:text/html;"))
            {
                return;
            }

            e.Cancel = true;

            Uri uri = new(e.Uri);

            // If it's a file-based anchor we converted, open the related file if possible
            if (uri.Authority == "browsing-file-host")
            {
                if (uri.Fragment?.StartsWith("#copy_") == true)
                {
                    var copyId = uri.Fragment.Substring(6);
                    _copyCodeAction(copyId);
                    return;
                }
            }
            else if (uri.IsAbsoluteUri && uri.Scheme.StartsWith("http"))
            {
                System.Diagnostics.Process.Start(uri.ToString());
            }
        }).FireAndForget();
    }

    private async Task NavigateToFragmentAsync(string fragmentId)
    {
        await WebView.ExecuteScriptAsync($"document.getElementById(\"{fragmentId}\").scrollIntoView(true)");
    }

    /// <summary>
    /// Adjust the file-based anchors so that they are navigable on the local file system
    /// </summary>
    /// <remarks>Anchors using the "file:" protocol appear to be blocked by security settings and won't work.
    /// If we convert them to use the "about:" protocol so that we recognize them, we can open the file in
    /// the <c>Navigating</c> event handler.</remarks>
    private async Task AdjustAnchorsAsync()
    {
        string script = @"
                for (const anchor of document.links) {
                    if (anchor != null && anchor.protocol == 'file:') {
                        var pathName = null, hash = anchor.hash;
                        if (hash != null) {
                            pathName = anchor.pathname;
                            anchor.hash = null;
                            anchor.pathname = '';
                        }
                        anchor.protocol = 'about:';

                        if (hash != null) {
                            if (pathName == null || pathName.endsWith('/')) {
                                pathName = 'blank';
                            }
                            anchor.pathname = pathName;
                            anchor.hash = hash;
                        }
                    }
                }";
        await WebView.ExecuteScriptAsync(script.Replace("\r", "\\r").Replace("\n", "\\n"));
    }

    private async Task<bool> IsHtmlTemplateLoadedAsync()
    {
        string hasContentResult = await WebView.ExecuteScriptAsync($@"document.getElementById(""___markdown-content___"") !== null;");
        return hasContentResult == "true";
    }

    public async Task UpdateBrowserAsync(string html)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await UpdateContentAsync(html);
        }
        catch
        {
        }

        async Task UpdateContentAsync(string html)
        {
            bool isInit = await IsHtmlTemplateLoadedAsync();
            if (isInit)
            {
                html = html.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"");
                await WebView.ExecuteScriptAsync($@"document.getElementById(""___markdown-content___"").innerHTML=""{html}"";");

                // Makes sure that any code blocks get syntax highlighted by Prism
                await WebView.ExecuteScriptAsync("Prism.highlightAll();");
                await WebView.ExecuteScriptAsync("mermaid.init(undefined, document.querySelectorAll('.mermaid'));");
                //await WebView.ExecuteScriptAsync("MathJax.Typeset(['.math']);");
                //await WebView.ExecuteScriptAsync("if (typeof onMarkdownUpdate == 'function') onMarkdownUpdate();");

                // Adjust the anchors after and edit
                await AdjustAnchorsAsync();
            }
            else
            {
                string htmlTemplate = GetHtmlTemplate();
                html = string.Format(CultureInfo.InvariantCulture, "{0}", html);
                html = htmlTemplate.Replace("[content]", html);
                WebView.NavigateToString(html);
            }
        }
    }

    public static string GetFolder()
    {
        string assembly = Assembly.GetExecutingAssembly().Location;
        return Path.GetDirectoryName(assembly);
    }

    private string GetHtmlTemplateFileNameFromResource()
    {
        return Path.Combine(GetFolder(), "Utils\\md-template.html");
    }

    private string GetHtmlTemplate()
    {
        bool useLightTheme = UseLightTheme();
        string css = ReadCSS(useLightTheme);
        string mermaidJsParameters = $"{{ 'securityLevel': 'loose', 'theme': '{(useLightTheme ? "forest" : "dark")}', startOnLoad: true, flowchart: {{ htmlLabels: false }} }}";

        string defaultHeadBeg = $@"
<head>
    <meta http-equiv=""X-UA-Compatible"" content=""IE=Edge"" />
    <meta charset=""utf-8"" />
    <base href=""http://{_mappedBrowsingFileVirtualHostName}/"" />
    <style>
        html, body {{margin: 0; padding-bottom:10px}}
        {css}
    </style>";

        string defaultContent = $@"
    <div id=""___markdown-content___"" class=""markdown-body"">
        [content]
    </div>
    <script src=""http://{_mappedMarkdownEditorVirtualHostName}/utils/prism.js""></script>
    <script src=""http://{_mappedMarkdownEditorVirtualHostName}/utils/mermaid.min.js""></script>
    <!--<script src=""http://{_mappedMarkdownEditorVirtualHostName}/utils/mathjax.js""></script>-->
    <script>
        mermaid.initialize({mermaidJsParameters});
    </script>
    ";

        string templateFileName = GetHtmlTemplateFileNameFromResource();
        string template = File.ReadAllText(templateFileName);
        return template
            .Replace("<head>", defaultHeadBeg)
            .Replace("[content]", defaultContent)
            .Replace("[title]", "Markdown Preview");

        string ReadCSS(bool useLightTheme)
        {
            string cssHighlightFile = useLightTheme ? "highlight.css" : "highlight-dark.css";

            string cssPrismFile = useLightTheme ? "prism.css" : "prism-dark.css";

            string folder = GetFolder();
            string cssHighlight = File.ReadAllText(Path.Combine(folder, "utils", cssHighlightFile));
            string cssPrism = File.ReadAllText(Path.Combine(folder, "utils", cssPrismFile));

            return cssHighlight + cssPrism;
        }

        bool UseLightTheme()
        {
            bool useLightTheme = AdvancedOptions.Instance.Theme == Theme.Light;

            if (AdvancedOptions.Instance.Theme == Theme.Automatic)
            {
                SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources[CommonControlsColors.TextBoxBackgroundBrushKey];
                ContrastComparisonResult contrast = ColorUtilities.CompareContrastWithBlackAndWhite(brush.Color);

                useLightTheme = contrast == ContrastComparisonResult.ContrastHigherWithBlack;
            }

            return useLightTheme;
        }
    }

}
