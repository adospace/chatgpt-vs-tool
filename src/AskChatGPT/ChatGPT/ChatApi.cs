using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AskChatGPT.ChatGPT.Models;

namespace AskChatGPT.ChatGPT;

class ChatApi
{
    readonly HttpClient _httpClientApi;

    private const string API_URL = "https://api.openai.com/v1/chat/completions";
    private const string MODEL = "gpt-3.5-turbo";

    private static readonly JsonSerializerSettings _defaultSerializerContext = 
        new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

    public ChatApi(string apiKey)
    {
        _httpClientApi = new HttpClient();
        _httpClientApi.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<IEnumerable<ChatMessage>> Prompt(IEnumerable<ChatMessage> messages)
    {
        var body = new 
        {
            model = MODEL,
            messages
        };

        var bodyAsString = JsonConvert.SerializeObject(body, _defaultSerializerContext);

        var response = await _httpClientApi.PostAsync(API_URL, new StringContent(bodyAsString, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        var responseAsString = await response.Content.ReadAsStringAsync();

        var responseModel = JsonConvert.DeserializeObject<ResponseModel>(responseAsString);

        if (responseModel == null)
        {
            throw new InvalidOperationException();
        }

        return responseModel.Choices.Select(_ => _.Message);
    }
}
