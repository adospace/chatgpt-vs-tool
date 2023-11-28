using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AskChatGPT.Options;

internal class OptionsProvider
{
    [ComVisible(true)]
    public class AdvancedOptions : BaseOptionPage<Options.AdvancedOptions> { }
}

public class AdvancedOptions : BaseOptionModel<AdvancedOptions>, IRatingConfig
{
    [DisplayName("Dark theme support")]
    [Description("Determines if the ChatGPT Helper tool window should render in dark mode when a dark Visual Studio theme is in use.")]
    [DefaultValue(Theme.Automatic)]
    [TypeConverter(typeof(EnumConverter))]
    public Theme Theme { get; set; } = Theme.Automatic;

    [DisplayName("Preferred source language")]
    [Description("Highlight code snippets according to the selected language.")]
    [DefaultValue("csharp")]
    public string PreferredSourceLanguage { get; set; } = "csharp";

    [DisplayName("OpenAI Model")]
    [Description("Select the OpenAI model used to query chatGPT.\r\nFor example: gpt-3.5-turbo, gpt-4, or gpt-4-32k (more info: https://platform.openai.com/docs/models)")]
    [DefaultValue("gpt-3.5-turbo")]
    public string GptModel { get; set; } = "gpt-3.5-turbo";
    
    [Browsable(false)]
    public int RatingRequests { get; set; }

    [DisplayName("OpenAI API Key")]
    [Description("Enter the API Key required to access OpenAI ChatGPT.")]
    public string ApiKey { get; set; } = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    [DisplayName("Sessions count")]
    [Description("Total number of active chat sessions.")]
    [DefaultValue("40")]
    public int SessionLimit { get; set; } = 40;
}

public enum Theme
{
    Automatic,
    Dark,
    Light,
}
