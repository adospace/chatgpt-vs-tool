using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using AskChatGPT.ChatGPT;
using AskChatGPT.ChatGPT.Models;
using AskChatGPT.Data;
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

    readonly static MarkupCodeHighlighter _markupCodeHighlighter = new ();

    public ChatToolWindowControlViewModel()
    {
        PromptCommand = new AsyncRelayCommand(PromptAsync, () => IsNotBusy);
        DeleteSessionCommand = new AsyncRelayCommand(DeleteSessionAsync, () => IsNotBusy);

        _browser = new BrowserWrapper(OnCopyCodeToClipboard);
        _browser.Initialized += Browser_Initialized;

        WeakReferenceMessenger.Default.Register<ShowChatGPTWindowMessage>(this);

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            _api = new ChatApi(apiKey);
        }
    }

    private async void Browser_Initialized(object sender, EventArgs e)
    {
        await UpdateMarkdownToBrowserAsync();
    }

    protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentSession))
        {
            await SelectCurrentSessionAsync();
            await UpdateMarkdownToBrowserAsync();
        }

        base.OnPropertyChanged(e);
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

    private ObservableCollection<ChatMessage> _messages = new();

    public ObservableCollection<ChatMessage> Messages
    {
        get => _messages;
        set => SetProperty(ref _messages, value);
    }

    private ObservableCollection<ChatSession> _sessions = [];

    public ObservableCollection<ChatSession> Sessions
    {
        get => _sessions;
        set => SetProperty(ref _sessions, value);
    }

    public ChatSession _currentSession = new ChatSession
    {
        Name = "New Chat Session",
        Created = DateTime.Now,
        TimeStamp = DateTime.Now,
    };

    public ChatSession CurrentSession
    {
        get => _currentSession;
        set
        {
            if (value != null)
            {
                SetProperty(ref _currentSession, value);
                OnPropertyChanged(nameof(CurrentSessionName));
            }
        }
    }

    private async Task SelectCurrentSessionAsync()
    {
        if (_currentSession != null && _currentSession.Id != 0)
        {
            var chatRepo = new ChatDbRepository();
            var messages = await chatRepo.GetMessagesAsync(_currentSession.Id);
            Messages = new ObservableCollection<ChatMessage>(messages);
        }
    }

    public string CurrentSessionName
    {
        get => _currentSession?.Name;
        set
        {
            if (_currentSession != null && value != typeof(ChatSession).FullName)
            {
                _currentSession.Name = value;
                OnPropertyChanged(nameof(CurrentSessionName));
            }
        }
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

    public IAsyncRelayCommand DeleteSessionCommand { get; }

    public async Task InitializeAsync()
    {
        var chatRepo = new ChatDbRepository();
        await chatRepo.MigrateAsync();

        var sessions = await chatRepo.GetSessionsAsync();
        
        Sessions = new ObservableCollection<ChatSession>(sessions);
        var firstSession = sessions.FirstOrDefault();
        if (firstSession != null)
        {
            CurrentSession = firstSession;

            var messages = await chatRepo.GetMessagesAsync(_currentSession.Id);
            
            Messages = new ObservableCollection<ChatMessage>(messages);

            await UpdateMarkdownToBrowserAsync();
        }
        else
        {
            Sessions.Add(CurrentSession);
        }
    }

    private async Task DeleteSessionAsync()
    {
        if (CurrentSession == null)
        {
            return;
        }

        var sessionIndex = Sessions.IndexOf(CurrentSession);

        var chatRepo = new ChatDbRepository();
        await chatRepo.DeleteSessionsAsync(CurrentSession.Id);

        Sessions.Remove(CurrentSession);

        if (sessionIndex == Sessions.Count)
        {
            sessionIndex--;
        }

        if (sessionIndex >= 0 &&
            sessionIndex <= Sessions.Count - 1)
        {
            CurrentSession = Sessions[sessionIndex];
        }
        else
        {
            CurrentSession = new ChatSession
            {
                Name = "New Chat Session",
                Created = DateTime.Now,
                TimeStamp = DateTime.Now,
            };
            Sessions.Add(CurrentSession);

            Messages = new ObservableCollection<ChatMessage>();
        }
    }

    public async Task PromptAsync()
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
            var promptMessage = new ChatMessageModel("user",
                string.IsNullOrWhiteSpace(CurrentSourceCode)
                ?
                CurrentCommandText
                :
                $@"{CurrentCommandText}
```
{CurrentSourceCode}
```
");

            //var replyMessages = await _api.Prompt(ChatMessageSessions
            //    .Reverse()
            //    .SelectMany(_ => new[] { _.Prompt }.Concat(_.Replies))
            //    .Concat(new[] { promptMessage }));

            //var newChatSession = new ChatMessageSession(promptMessage, replyMessages.ToArray());

            //ChatMessageSessions.Add(newChatSession);

            //foreach (var reply in replyMessages)
            //{
            //    ChatMessages.Insert(0, reply);
            //}

            var replyMessages = await _api.Prompt(
                _messages.Select(_ => new ChatMessageModel(_.Role, _.Content)).Concat(new[] { promptMessage }));

            var chatRepo = new ChatDbRepository();

            if (_currentSession.Id == 0)
            {
                _sessions.Remove(_currentSession);

                var sessionName = CurrentCommandText
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(_ => !string.IsNullOrWhiteSpace(_)) ?? "New Chat Session";

                Sessions.Add(new ChatSession
                {
                    Name = sessionName,
                    Created = DateTime.Now,
                    TimeStamp = DateTime.Now
                });

                CurrentSession = Sessions.Last();

                await chatRepo.InsertSessionAsync(CurrentSession);
            }
            else
            {
                _currentSession.TimeStamp = DateTime.Now;
                await chatRepo.UpdateSessionsAsync(_currentSession);
            }

            var newPromptMessage = new ChatMessage
            {
                Role = "user",
                Content = promptMessage.Content,
                SessionId = _currentSession.Id
            };

            //chatDbContext.Messages.Add(newPromptMessage);
            await chatRepo.InsertMessagesAsync(newPromptMessage);
            _messages.Add(newPromptMessage);

            var newMessages = new List<ChatMessage>();
            foreach (var reply in replyMessages)
            {
                var newReplyMessage = new ChatMessage
                {
                    Role = reply.Role,
                    Content = reply.Content,
                    SessionId = _currentSession.Id
                };

                newMessages.Add(newReplyMessage);

                _messages.Add(newReplyMessage);
            }

            await chatRepo.InsertMessagesAsync(newMessages.ToArray());
            //await chatDbContext.SaveChangesAsync();

            //ChatMessages.Insert(0, promptMessage);

            await UpdateMarkdownToBrowserAsync();

            //SaveCurrentCommandToRecentList();

            CurrentCommandText = string.Empty;

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

    private async Task UpdateMarkdownToBrowserAsync()
    {
        if (!_browser.IsInitialized)
        {
            return;
        }

        MessagesAsMarkdown = _markupCodeHighlighter.TransformMarkdown(
            string.Join(Environment.NewLine + Environment.NewLine,
            Messages.Select(_ =>
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

    internal async Task NewMessageAsync()
    {
        //ChatSessions.Clear();
        //ChatMessages.Clear();
        Sessions.Add(new ChatSession
        {
            Name = "New Chat Session"
        });
        CurrentSession = Sessions.Last();
        Messages.Clear();

        _markupCodeHighlighter.ClearCopyCodeLinks();

        await UpdateMarkdownToBrowserAsync();
    }

    //private void SaveCurrentCommandToRecentList()
    //{
    //    var currentText = Regex.Replace(_currentCommandText.Trim(), @"\s+", " ");

    //    if (string.IsNullOrWhiteSpace(currentText))
    //    {
    //        return;
    //    }

    //    var existingCommand = RecentCommands
    //        .FirstOrDefault(_ => string.Compare(_, currentText, true) == 0);

    //    if (existingCommand != null)
    //    {
    //        RecentCommands.Remove(existingCommand);
    //    }

    //    RecentCommands.Insert(0, currentText);
    //    if (RecentCommands.Count == 21)
    //    {
    //        RecentCommands.RemoveAt(20);
    //    }

    //    CurrentCommandText = string.Empty;
    //    CurrentSourceCode = string.Empty;

    //    File.WriteAllText(
    //        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "recent_commands.json"),
    //        JsonConvert.SerializeObject(RecentCommands));
    //}

    void OnCopyCodeToClipboard(string copyId)
    {
        _markupCodeHighlighter.Copy(copyId);
    }

    public void Receive(ShowChatGPTWindowMessage message)
    {
        if (message.Value?.CodeChunk != null)
        {
            CurrentSourceCode = message.Value.CodeChunk;
        }
    }    
}

//record ChatMessageSession(ChatMessageModel Prompt, ChatMessageModel[] Replies);
