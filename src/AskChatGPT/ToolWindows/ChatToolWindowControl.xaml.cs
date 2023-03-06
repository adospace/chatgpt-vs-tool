using AskChatGPT.ToolWindows;
using System.Windows;
using System.Windows.Controls;

namespace AskChatGPT
{
    public partial class ChatToolWindowControl : UserControl
    {
        public ChatToolWindowControl()
        {
            InitializeComponent();
        }

        private async void ClearAllLink_Click(object sender, RoutedEventArgs e)
        {
            await ((ChatToolWindowControlViewModel)DataContext).ClearAllMessages();
        }
    }
}