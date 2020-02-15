using Newtonsoft.Json;

namespace Url2Kindle.Functions
{
    internal class Chat
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}
