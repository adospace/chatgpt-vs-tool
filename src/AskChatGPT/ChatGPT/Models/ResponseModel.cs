using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AskChatGPT.ChatGPT.Models;

/*
{
  "id": "chatcmpl-6q3Ge0ngjcpy47XXITLq3Nfs9mnvK",
  "object": "chat.completion",
  "created": 1677863636,
  "model": "gpt-3.5-turbo-0301",
  "usage": {
    "prompt_tokens": 9,
    "completion_tokens": 12,
    "total_tokens": 21
  },
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "\n\nHello there! How may I assist you today?"
      },
      "finish_reason": "stop",
      "index": 0
    }
  ]
}
 */

record ResponseModel(
    [property: JsonProperty("id")] string Id,
    [property: JsonProperty("object")] string Object,
    [property: JsonProperty("created")] int Created,
    [property: JsonProperty("choices")] ResponseChoiceModel[] Choices,
    [property: JsonProperty("usage")] UsageModel Usage
    );

record ResponseChoiceModel(
    [property: JsonProperty("index")] string Index,
    [property: JsonProperty("message")] ChatMessageModel Message,
    [property: JsonProperty("finish_reason")] string FinishReason
);

record UsageModel(
    [property: JsonProperty("prompt_tokens")] int PromptTokens,
    [property: JsonProperty("completion_tokens")] int CompletionTokens,
    [property: JsonProperty("total_tokens")] int TotalTokens
);