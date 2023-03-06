using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using AskChatGPT.ChatGPT;
using AskChatGPT.ChatGPT.Models;
using AskChatGPT.Options;
using AskChatGPT.Utils;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EnvDTE;
using Markdig;
using Markdig.Renderers;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;

namespace AskChatGPT.ToolWindows;

partial class ChatToolWindowControlViewModel : ObservableObject, IRecipient<ShowChatGPTWindowMessage>
{
    readonly ChatApi _api = null;
    readonly MarkdownToHtmlConverter _markdownToHtmlConverter = new();

    readonly BrowserWrapper _browser;

    readonly static string _recentCommandsFilePath
        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "recent_commands.json");

    readonly static Dictionary<string, string> _copyCode = new();

    public ChatToolWindowControlViewModel()
    {
        PromptCommand = new AsyncRelayCommand(Prompt, () => IsNotBusy);

        _browser = new BrowserWrapper(OnCopyCodeToClipbard);

        WeakReferenceMessenger.Default.Register<ShowChatGPTWindowMessage>(this);

        if (File.Exists(_recentCommandsFilePath))
        {
            _recentCommands = new ObservableCollection<string>(
                JsonConvert.DeserializeObject<string[]>(File.ReadAllText(_recentCommandsFilePath))
                    .Where(_ => !string.IsNullOrWhiteSpace(_))
                    .ToArray()
                );
        }

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            _api = new ChatApi(apiKey);
        }
    }

    public WebView2 BrowserView => _browser.WebView;

    public Visibility OpenAIMissingVisibility => _api == null ? Visibility.Visible : Visibility.Collapsed;

    private string _currentCommandText;

    public string CurrentCommandText
    {
        get => _currentCommandText;
        set => SetProperty(ref _currentCommandText, value);
    }

    private ObservableCollection<string> _recentCommands = new();

    public ObservableCollection<string> RecentCommands
    {
        get => _recentCommands;
    }

    private string _currentSourceCode;

    public string CurrentSourceCode
    {
        get => _currentSourceCode;
        set => SetProperty(ref _currentSourceCode, value);
    }

    private ObservableCollection<ChatMessage> _chatMessages = new();

    public ObservableCollection<ChatMessage> ChatMessages
    {
        get => _chatMessages;
        set => SetProperty(ref _chatMessages, value);
    }

    private ObservableCollection<ChatMessageSession> _chatMessageSessions = new();

    public ObservableCollection<ChatMessageSession> ChatMessageSessions
    {
        get => _chatMessageSessions;
        set => SetProperty(ref _chatMessageSessions, value);
    }

    private string _messagesAsMarkdown;

    public string MessagesAsMarkdown
    {
        get => _messagesAsMarkdown;
        set => SetProperty(ref _messagesAsMarkdown, value);
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            OnPropertyChanged(nameof(IsNotBusy));
            OnPropertyChanged(nameof(IsBusyIndicatorVisibility));
            OnPropertyChanged(nameof(IsNotBusyIndicatorVisibility));
        }
    }

    public bool IsNotBusy => !IsBusy;
    public Visibility IsBusyIndicatorVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsNotBusyIndicatorVisibility => !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    private string _errorMessage;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            SetProperty(ref _errorMessage, value);
            OnPropertyChanged(nameof(ErrorMessageVisibility));
        }
    }

    public Visibility ErrorMessageVisibility => string.IsNullOrEmpty(_errorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand PromptCommand { get; }

    public async Task Prompt()
    {
        if (_api == null || !_browser.IsInitialized)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentCommandText))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var promptMessage = new ChatMessage("user",
                string.IsNullOrWhiteSpace(CurrentSourceCode)
                ?
                CurrentCommandText
                :
                $@"{CurrentCommandText}
```
{CurrentSourceCode}
```
");

            var replyMessages = await _api.Prompt(ChatMessageSessions
                .Reverse()
                .SelectMany(_ => new[] { _.Prompt }.Concat(_.Replies))
                .Concat(new[] { promptMessage }));

            var newChatSession = new ChatMessageSession(promptMessage, replyMessages.ToArray());

            ChatMessageSessions.Add(newChatSession);

            foreach (var reply in replyMessages)
            {
                ChatMessages.Insert(0, reply);
            }

            ChatMessages.Insert(0, promptMessage);

            await UpdateMarkdownToBrowser();

            SaveCurrentCommandToRecentList();

            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UpdateMarkdownToBrowser()
    {
        if (!_browser.IsInitialized)
        {
            return;
        }

        MessagesAsMarkdown = SetupCopyCodeLinks(
            string.Join(Environment.NewLine + Environment.NewLine,
            ChatMessages.Select(_ =>
            {
                if (_.Role != "user")
                {
                    return $@"{_.Content}

---
";
                }
                else //if (_.Role == "user")
                {
                    return $"You: {_.Content}";
                }
            })));

        var html = await _markdownToHtmlConverter.ConvertToHtml(MessagesAsMarkdown);

        await _browser.UpdateBrowserAsync(html);
    }

    internal async Task ClearAllMessages()
    {
        ChatMessageSessions.Clear();
        ChatMessages.Clear();
        _copyCode.Clear();

        await UpdateMarkdownToBrowser();
    }

    private void SaveCurrentCommandToRecentList()
    {
        var currentText = Regex.Replace(_currentCommandText.Trim(), @"\s+", " ");

        if (string.IsNullOrWhiteSpace(currentText))
        {
            return;
        }

        var existingCommand = RecentCommands
            .FirstOrDefault(_ => string.Compare(_, currentText, true) == 0);

        if (existingCommand != null)
        {
            RecentCommands.Remove(existingCommand);
        }

        RecentCommands.Insert(0, currentText);
        if (RecentCommands.Count == 21)
        {
            RecentCommands.RemoveAt(20);
        }

        CurrentCommandText = string.Empty;
        CurrentSourceCode = string.Empty;

        File.WriteAllText(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "recent_commands.json"),
            JsonConvert.SerializeObject(RecentCommands));
    }

    private string SetupCopyCodeLinks(string text)
    {
        int index = 0;
        while (true)
        {
            int startingIndexOfCode = text.IndexOf("```", index);
            if (startingIndexOfCode == -1)
            {
                break;
            }

            int endingIndexOfCode = text.IndexOf("```", startingIndexOfCode + 3);
            if (endingIndexOfCode == -1)
            {
                break;
            }

            var codeToCopy = text
                .Substring(startingIndexOfCode + 3, endingIndexOfCode - startingIndexOfCode - 3)
                .TrimStart('\r').TrimStart('\n')
                .TrimEnd('\n').TrimEnd('\r')
                ;

            if (!string.IsNullOrWhiteSpace(codeToCopy))
            {
                string sourceCopyId = Guid.NewGuid().ToString("N");
                _copyCode.Add(sourceCopyId, codeToCopy);

                text = text.Remove(startingIndexOfCode, 3);
                var linkToCopyText = $@"
[Copy](#copy_{sourceCopyId})

```{AdvancedOptions.Instance.PreferredSourceLanguage}";
                text = text.Insert(startingIndexOfCode, linkToCopyText);

                index = text.IndexOf("```", startingIndexOfCode + linkToCopyText.Length) + 3;
                continue;
            }

            index = endingIndexOfCode + 3;
        }

        return text;
    }

    void OnCopyCodeToClipbard(string copyId)
    {
        if (_copyCode.TryGetValue(copyId, out var codeToCopy))
        {
            Clipboard.SetText(codeToCopy);
        }
    }

    public void Receive(ShowChatGPTWindowMessage message)
    {
        if (message.Value?.CodeChunk != null)
        {
            CurrentSourceCode = message.Value.CodeChunk;
        }
    }    
}

record ChatMessageSession(ChatMessage Prompt, ChatMessage[] Replies);
