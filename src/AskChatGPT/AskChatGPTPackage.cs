global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using AskChatGPT.Options;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

namespace AskChatGPT
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideToolWindow(typeof(ChatToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
    [ProvideToolWindowVisibility(typeof(ChatToolWindow.Pane), VSConstants.UICONTEXT.SolutionHasSingleProject_string)]
    [ProvideToolWindowVisibility(typeof(ChatToolWindow.Pane), VSConstants.UICONTEXT.SolutionHasMultipleProjects_string)]
    [ProvideToolWindowVisibility(typeof(ChatToolWindow.Pane), VSConstants.UICONTEXT.NoSolution_string)]
    [ProvideToolWindowVisibility(typeof(ChatToolWindow.Pane), VSConstants.UICONTEXT.EmptySolution_string)]

    [ProvideOptionPage(typeof(OptionsProvider.AdvancedOptions), "ChatGPT", "ChatGPT Helper Tool", 0, 0, true, new[] { "help", "chat", "gpt" })]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.AskChatGPTString)]
    public sealed class AskChatGPTPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            this.RegisterToolWindows();
        }
    }
}