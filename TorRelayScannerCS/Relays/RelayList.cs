using System.Text.Json.Serialization;

namespace Tor_relay_scanner_CS.Relays
{
    /**
     * Most of the fields are completely not necessary to load into memory
     **/
    [Serializable]
    public struct RelayList
    {

        [JsonIgnore]
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonIgnore]
        [JsonPropertyName("build_revision")]
        public string BuildRevision { get; set; }

        //[JsonIgnore]
        [JsonPropertyName("relays_published")]
        public string RelaysPublishDate { get; set; }

        [JsonIgnore]
        [JsonPropertyName("bridges_published")]
        public string BridgesPublishDate { get; set; }

        [JsonPropertyName("relays")]
        public Relay[] Relays { get; set; }

        [JsonIgnore]
        [JsonPropertyName("bridges")]
        public Bridge[] Bridges { get; set; }
    }
}
