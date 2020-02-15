using Newtonsoft.Json;

namespace Url2Kindle.Functions
{
    internal class BotMessage
    {
        [JsonProperty("message")]
        public Message Message { get; set; }

        public Chat Chat
        {
            get
            {
                return Message?.Chat;
            }
        }
    }
}
