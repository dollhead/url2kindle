using Newtonsoft.Json;

namespace Url2Kindle.Functions
{
    internal class Message
    {
        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("date")]
        public long Date { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("chat")]
        public Chat Chat { get; set; }

        [JsonProperty("photo")]
        public dynamic Photo { get; set; }

        [JsonProperty("document")]
        public dynamic Document { get; set; }
    }
}
