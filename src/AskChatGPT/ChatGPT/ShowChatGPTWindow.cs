using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AskChatGPT.ChatGPT;

record ShowChatGPTWindow(string CodeChunk);

class ShowChatGPTWindowMessage : ValueChangedMessage<ShowChatGPTWindow>
{
    public ShowChatGPTWindowMessage(ShowChatGPTWindow _) : base(_)
    {
    }
}