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
using Kindlegen;
using Kindlegen.Models;
using NReadability;
using Telegram.Bot.Types.InputFiles;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Url2Kindle.Functions
{
    public static class ProcessUserMessage
    {
        private static readonly string TelegramApiToken = System.Environment.GetEnvironmentVariable("TelegramApiToken");

        private static readonly HttpClient HttpClient = new HttpClient();
        
        private static readonly TelegramBotClient BotClient = new TelegramBotClient(TelegramApiToken, HttpClient);

        private static readonly NReadabilityTranscoder Transcoder =
            new NReadabilityTranscoder(ReadingStyle.Ebook, ReadingMargin.Medium, ReadingSize.Medium);

        [FunctionName("ProcessUserMessage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestContent = await GetRequestContent(req.HttpContext);
            var botMessage = JsonConvert.DeserializeObject<BotMessage>(requestContent);

            long chatId = botMessage.Chat.Id;

            log.LogInformation($"Received a message from user {chatId}");

            string messageText = botMessage.Message.Text;
            if (!Uri.TryCreate(messageText, UriKind.Absolute, out var uri))
            {
                log.LogWarning($"Failed to parse URI in {messageText}");

                await BotClient.SendTextMessageAsync(chatId, "Please, send a valid uri.");
                return new OkResult();
            }

            await BotClient.SendChatActionAsync(chatId, ChatAction.Typing);

            string html;
            using (var response = await HttpClient.GetAsync(uri))
            {
                if (!response.IsSuccessStatusCode)
                {
                    log.LogWarning($"Failed to open URI {uri}");

                    await BotClient.SendTextMessageAsync(chatId, "Failed to open provided uri.");
                    return new OkResult();
                }

                html = await response.Content.ReadAsStringAsync();
            }

            var input = new TranscodingInput(html);
            var transcodingResult = Transcoder.Transcode(input);
            if (!transcodingResult.ContentExtracted)
            {
                var message = "Failed to extract content.";
                log.LogWarning(message);
                await BotClient.SendTextMessageAsync(chatId, "message");
                return new OkResult();
            }

            await BotClient.SendTextMessageAsync(chatId, transcodingResult.ExtractedTitle);

            var fileId = Guid.NewGuid().ToString();
            var filePath = Path.Combine(Path.GetTempPath(), $"{fileId}.html");

            using (var writer = new StreamWriter(filePath))
            {
                await writer.WriteAsync(transcodingResult.ExtractedContent);
            }

            string resultFilePath = ConvertToMobi(log, filePath);

            using (var fs = File.OpenRead(resultFilePath))
            {
                InputOnlineFile inputOnlineFile = new InputOnlineFile(fs, $"{transcodingResult.ExtractedTitle}.mobi");
                await BotClient.SendDocumentAsync(chatId, inputOnlineFile);
            }

            log.LogInformation($"Successfully converted uri {uri} to kindle book");

            return new OkResult();

            async Task<string> GetRequestContent(HttpContext context)
            {
                using (var reader = new StreamReader(context.Request.Body))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        private static string ConvertToMobi(ILogger log, string filePath)
        {
            var resultFilePath = Path.ChangeExtension(filePath, "mobi");
            try
            {
                var kindleGen = new Process();
                kindleGen.StartInfo.UseShellExecute = false;
                kindleGen.StartInfo.RedirectStandardOutput = true;
                kindleGen.StartInfo.FileName = Path.Combine(Path.GetTempPath(), "kindlegen");

                var tempPathLinux = Path.Combine(Path.GetTempPath(), "kindlegen");
                if (!File.Exists(tempPathLinux))
                    File.Copy("kindlegen", Path.Combine(Path.GetTempPath(), "kindlegen"), false);
                var tempPathWindows = Path.Combine(Path.GetTempPath(), "kindlegen.exe");
                if (!File.Exists(tempPathWindows))
                    File.Copy("kindlegen.exe", Path.Combine(Path.GetTempPath(), "kindlegen.exe"), false);

                var arguments = $"{filePath} -o \"{Path.GetFileName(resultFilePath)}\"";
                kindleGen.StartInfo.Arguments = Encoding.Default.GetString(Encoding.UTF8.GetBytes(arguments));
                kindleGen.Start();
                kindleGen.WaitForExit();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to convert file");
            }

            return resultFilePath;
        }
    }
}
