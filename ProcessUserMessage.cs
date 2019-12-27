using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using System.Net.Http;

namespace Url2Kindle.Functions
{
    public static class ProcessUserMessage
    {
        private static string telegramApiToken = System.Environment.GetEnvironmentVariable("TelegramApiToken");

        private static HttpClient httpClient = new HttpClient();
        
        private static TelegramBotClient botClient = new TelegramBotClient(telegramApiToken, httpClient);

        [FunctionName("ProcessUserMessage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [Blob("uri-htmls/{rand-guid}.html", FileAccess.Write)] Stream htmlBlobStream,
            ILogger log)
        {
            var requestContent = await GetRequestContent(req.HttpContext);
            var botMessage = JsonConvert.DeserializeObject<BotMessage>(requestContent);

            if (!Uri.TryCreate(botMessage.Message.Text, UriKind.Absolute, out var uri))
            {
                await botClient.SendTextMessageAsync(botMessage.Chat.Id, "Please, send a valid uri.");
                return new OkResult();
            }

            using(var response = await httpClient.GetStreamAsync(uri))
            {
                await response.CopyToAsync(htmlBlobStream);
            }

            await botClient.SendTextMessageAsync(botMessage.Chat.Id, "Starting document processing...");
            await botClient.SendChatActionAsync(botMessage.Chat.Id, ChatAction.Typing);
            
            return new OkResult();

            async Task<string> GetRequestContent(HttpContext context)
            {
                using (var reader = new StreamReader(context.Request.Body))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }
    }

    internal class BotMessage
    {
        [JsonProperty("update_id")]
        public long UpdateId { get; set; }

        [JsonProperty("message")]
        public Message Message { get; set; }

        [JsonProperty("callback_query")]
        public CallbackQuery CallbackQuery { get; set; }

        public Chat Chat
        {
            get
            {
                return Message?.Chat ?? CallbackQuery?.Message?.Chat;
            }
        }

        public MessageSender From
        {
            get
            {
                return Message?.From ?? CallbackQuery?.From;
            }
        }
    }

    internal class Message
    {
        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("date")]
        public long Date { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("from")]
        public MessageSender From { get; set; }

        [JsonProperty("chat")]
        public Chat Chat { get; set; }

        [JsonProperty("photo")]
        public dynamic Photo { get; set; }

        [JsonProperty("document")]
        public dynamic Document { get; set; }
    }

    internal class MessageSender
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("is_bot")]
        public bool IsBot { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("language_code")]
        public string LanguageCode { get; set; }
    }

        internal class Chat
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    internal class CallbackQuery
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("chat_instance")]
        public string ChatInstance { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("from")]
        public MessageSender From { get; set; }

        [JsonProperty("message")]
        public Message Message { get; set; }
    }
}
