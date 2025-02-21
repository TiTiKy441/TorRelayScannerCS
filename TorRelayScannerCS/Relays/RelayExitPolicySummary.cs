using System.Text.Json.Serialization;

namespace Tor_relay_scanner_CS.Relays
{
    [Serializable]
    public struct RelayExitPolicySummary
    {

        [JsonPropertyName("accept")]
        public string[] Accept { get; set; }


        [JsonPropertyName("reject")]
        public string[] Reject { get; set; }

    }
}
