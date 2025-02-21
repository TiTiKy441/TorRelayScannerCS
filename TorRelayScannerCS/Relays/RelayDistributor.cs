using System.Text.Json;

namespace Tor_relay_scanner_CS.Relays
{

    /**
     * Please be patient
     * I have autism
     */
    public class RelayDistributor
    {

        public static RelayDistributor Instance { get; private set; }

        public RelayList RelayInfo { get; private set; }

        public List<Relay> ExitRelays { get; private set; } = new();
        public List<Relay> GuardRelays { get; private set; } = new();

        public string FilePath { get; private set; }

        private RelayDistributor(string filePath)
        {
            FilePath = filePath;
            Read();
        }

        public static RelayDistributor Initialize(string filePath)
        {
            if (Instance != null) throw new InvalidOperationException("RelayDistributor was already initialized");
            Instance = new RelayDistributor(filePath);
            return Instance;
        }

        public static RelayDistributor? TryInitialize(string filePath)
        {
            if (Instance != null) throw new InvalidOperationException("RelayDistributor was already initialized");
            try
            {
                Instance = new RelayDistributor(filePath);
                return Instance;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Reset()
        {
            Instance = null;
        }

        public void ChangePath(string filePath)
        {
            FilePath = filePath;
            Read();
        }

        public void Read()
        {
            using (FileStream stream = File.OpenRead(FilePath))
            {
                using (JsonDocument doc = JsonDocument.Parse(stream))
                {
                    RelayInfo = doc.Deserialize<RelayList>();
                }
            }
            try
            {
                ExitRelays = GetRelaysWithFlags(new string[1] { "Exit", }).ToList();
                GuardRelays = GetRelaysWithFlags(new string[1] { "Guard", }).ToList();
            }
            catch (Exception)
            {
            }
        }

        public Relay[] GetRelaysWithFlags(string[] flags)
        {
            return RelayInfo.Relays.Where((x, i) => flags.All(y => x.Flags.Contains(y))).ToArray();
        }

        public Relay[] GetRelaysWithFlag(string flag)
        {
            return RelayInfo.Relays.Where((x, i) => x.Flags.Contains(flag)).ToArray();
        }

        public Relay[] GetRelaysFromCountries(string[] countries)
        {
            return RelayInfo.Relays.Where((x, i) => countries.Contains(x.Country)).ToArray();
        }

        public Relay[] GetRelaysFromCountry(string country)
        {
            return RelayInfo.Relays.Where((x, i) => x.Country.Contains(country)).ToArray();
        }

        public Relay? FindRelayByIp(string ip)
        {
            return RelayInfo.Relays.Where((x, i) => x.Addresses.Contains(ip)).FirstOrNull();
        }

        public Relay? FindRelayByFingerprint(string fingerprint)
        {
            return RelayInfo.Relays.Where((x, i) => x.Fingerprint.Contains(fingerprint)).FirstOrNull();
        }

        public Relay[] GetRelaysWithoutFlags(string[] flags)
        {
            return RelayInfo.Relays.Where((x, i) => !flags.Any(y => x.Flags.Contains(y))).ToArray();
        }

        public Relay[] GetRelaysWithoutFlag(string flag)
        {
            return RelayInfo.Relays.Where((x, i) => !x.Flags.Contains(flag)).ToArray();
        }
    }
}
