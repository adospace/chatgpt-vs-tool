using AskChatGPT.ChatGPT;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.VisualStudio.Text;
using System.Linq;

namespace AskChatGPT
{
    [Command(PackageIds.MyCommand)]
    internal sealed class ChatToolWindowCommand : BaseCommand<ChatToolWindowCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ChatToolWindow.ShowAsync();

            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            NormalizedSnapshotSpanCollection selections = docView.TextView?.Selection.SelectedSpans;

            if (selections == null)
                return;

            var selectedCode = selections.FirstOrDefault().GetText();

            WeakReferenceMessenger.Default.Send(
                new ShowChatGPTWindowMessage(
                    new ShowChatGPTWindow(selectedCode)));
        }
    }
}
