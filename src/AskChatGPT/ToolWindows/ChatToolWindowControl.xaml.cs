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

            this.Loaded += ChatToolWindowControl_Loaded;
        }

        private async void ChatToolWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            await ((ChatToolWindowControlViewModel)DataContext).InitializeAsync();
        }

        private async void NewSession_Click(object sender, RoutedEventArgs e)
        {
            await ((ChatToolWindowControlViewModel)DataContext).NewMessageAsync();
        }


    }
}