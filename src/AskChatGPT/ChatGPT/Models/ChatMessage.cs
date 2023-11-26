using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AskChatGPT.ChatGPT.Models;

record ChatMessageModel([property: JsonProperty("role")] string Role, [property: JsonProperty("content")] string Content);
