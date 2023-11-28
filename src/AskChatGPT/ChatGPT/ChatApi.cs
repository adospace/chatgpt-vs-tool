using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AskChatGPT.ChatGPT.Models;
using AskChatGPT.Options;

namespace AskChatGPT.ChatGPT;

class ChatApi
{
    readonly HttpClient _httpClientApi;

    private const string API_URL = "https://api.openai.com/v1/chat/completions";

    private static readonly JsonSerializerSettings _defaultSerializerContext = 
        new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

    public ChatApi()
    {
        _httpClientApi = new HttpClient();
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(AdvancedOptions.Instance.ApiKey);

    public async Task<IEnumerable<ChatMessageModel>> Prompt(IEnumerable<ChatMessageModel> messages)
    {
        _httpClientApi.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdvancedOptions.Instance.ApiKey);

        var body = new 
        {
            model = AdvancedOptions.Instance.GptModel,
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
