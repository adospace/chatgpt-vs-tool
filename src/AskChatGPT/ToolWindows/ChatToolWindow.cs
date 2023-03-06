using Microsoft.VisualStudio.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AskChatGPT;

public class ChatToolWindow : BaseToolWindow<ChatToolWindow>
{
    public override string GetTitle(int toolWindowId) => "ChatGPT Helper Tool";

    public override Type PaneType => typeof(Pane);

    public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        return Task.FromResult<FrameworkElement>(new ChatToolWindowControl());
    }

    [Guid("789371c0-258b-43da-a1cf-86e5222ae2ed")]
    internal class Pane : ToolkitToolWindowPane
    {
        public Pane()
        {
            BitmapImageMoniker = KnownMonikers.ToolWindow;
        }
    }
}