using AskChatGPT.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AskChatGPT.Utils;

public class MarkupCodeHighlighter
{
    readonly Dictionary<string, string> _copyCode = new();

    public MarkupCodeHighlighter()
    {
    }

    internal void ClearCopyCodeLinks()
    {
        _copyCode.Clear();
    }

    internal void Copy(string copyId)
    {
        if (_copyCode.TryGetValue(copyId, out var codeToCopy))
        {
            Clipboard.SetText(codeToCopy);
        }
    }

    public string TransformMarkdown(string text)
    {
        using StringReader reader = new(text);
        
        string line;
        const string codeSeparator = "```";
        bool insideCode = false;
        string sourceCopyId = null;
        StringBuilder codeToCopy = new();

        StringBuilder outputText = new();

        while ((line = reader.ReadLine()) != null)
        {
            int indexOfSeparator = line.IndexOf(codeSeparator);
            if (indexOfSeparator > -1)
            {
                if (!insideCode)
                {
                    insideCode = true;
                    
                    sourceCopyId = Guid.NewGuid().ToString("N");

                    var linkToCopyText = $@"
[Copy](#copy_{sourceCopyId})
";

                    if (line.Length == indexOfSeparator + 3)
                    {
                        line = line.Insert(indexOfSeparator + 3, AdvancedOptions.Instance.PreferredSourceLanguage);
                    }

                    outputText.AppendLine(linkToCopyText);
                }
                else
                {
                    insideCode = false;
                    if (sourceCopyId != null)
                    {
                        _copyCode.Add(sourceCopyId, codeToCopy.ToString());
                    }
                    
                    codeToCopy.Clear();
                }
                    
                outputText.AppendLine(line);
            }
            else
            {
                if (insideCode)
                {
                    codeToCopy.AppendLine(line);
                }

                outputText.AppendLine(line);
            }
        }

        return outputText.ToString();
    }

//    public string TransformMarkdown(string text)
//    {
//        int index = 0;
//        while (true)
//        {
//            int startingIndexOfCode = text.IndexOf($"```", index);
//            if (startingIndexOfCode == -1)
//            {
//                break;
//            }

    //            int endingIndexOfCode = text.IndexOf("```", startingIndexOfCode + 3);
    //            if (endingIndexOfCode == -1)
    //            {
    //                break;
    //            }

    //            int startIndexOfCodeToCopy = text.IndexOfAny(new[] { '\r', '\n' }, startingIndexOfCode);

    //            if (startIndexOfCodeToCopy == -1)
    //            {
    //                break;
    //            }

    //            bool languageSpecPresent = startIndexOfCodeToCopy > startingIndexOfCode + 3;

    //            if (text[startIndexOfCodeToCopy] == '\r' &&
    //                startIndexOfCodeToCopy < text.Length-1 &&
    //                text[startIndexOfCodeToCopy + 1]=='\n')
    //            {
    //                startIndexOfCodeToCopy+=2;
    //            }
    //            else
    //            {
    //                startingIndexOfCode++;
    //            }

    //            var codeToCopy = text
    //                .Substring(startIndexOfCodeToCopy, endingIndexOfCode - startIndexOfCodeToCopy)
    //                ;

    //            if (!string.IsNullOrWhiteSpace(codeToCopy))
    //            {
    //                string sourceCopyId = Guid.NewGuid().ToString("N");
    //                _copyCode.Add(sourceCopyId, codeToCopy);

    //                if (!languageSpecPresent)
    //                {
    //                    text = text.Insert(startingIndexOfCode + 3, AdvancedOptions.Instance.PreferredSourceLanguage);
    //                }

    //                var linkToCopyText = $@"
    //[Copy](#copy_{sourceCopyId})
    //";
    //                text = text.Insert(startingIndexOfCode, linkToCopyText);

    //                index = text.IndexOf("```", startingIndexOfCode + linkToCopyText.Length + (languageSpecPresent ? 0 : AdvancedOptions.Instance.PreferredSourceLanguage.Length)) + 3;
    //                continue;
    //            }

    //            index = endingIndexOfCode + 3;
    //        }

    //        return text;
    //    }


}
