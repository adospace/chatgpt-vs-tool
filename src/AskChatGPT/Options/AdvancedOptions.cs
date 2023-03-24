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
    [Description("Select the OpenAI model used to query chatGPT.")]
    [DefaultValue("gpt-3.5-turbo")]
    public string GptModel { get; set; } = "gpt-3.5-turbo";
    
    [Browsable(false)]
    public int RatingRequests { get; set; }


}

public enum Theme
{
    Automatic,
    Dark,
    Light,
}
