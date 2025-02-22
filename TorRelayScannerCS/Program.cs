using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Tor_relay_scanner_CS.Relays;

namespace Tor_relay_scanner_CS
{
    class Program
    {

        public static readonly List<string> mirrors = new()
        {
            "https://onionoo.torproject.org/details?type=relay&running=true&fields=fingerprint,or_addresses,country",
            "https://icors.vercel.app/?https%3A//onionoo.torproject.org/details%3Ftype%3Drelay%26running%3Dtrue%26fields%3Dfingerprint%2Cor_addresses%2Ccountry",
            "https://github.com/ValdikSS/tor-onionoo-mirror/raw/master/details-running-relays-fingerprint-address-only.json",
            "https://bitbucket.org/ValdikSS/tor-onionoo-mirror/raw/master/details-running-relays-fingerprint-address-only.json",
        };

        public static readonly List<string> WorkingRelayStrings = new();

        public static uint Goal;

        /// <summary>
        /// Automatically retrieves information about tor relays and scans them for reachability
        /// </summary>
        /// <param name="n">The number of concurrent relays tested</param>
        /// <param name="g">Test until at least this number of working relays are found</param>
        /// <param name="c">Include only following countries for testing, exclude by adding '-'. Example: nl,de (only netherlands and germany) Example exclude: -nl (not netherlands)</param>
        /// <param name="timeout">Socket connection timeout in milliseconds</param>
        /// <param name="o">Output reachable relays to file</param>
        /// <param name="torrc">Output reachable relays in torrc format (with "Bridge" prefix)</param>
        /// <param name="proxy">Set proxy for onionoo information download. Format: http://user:pass@host:port; socks5h://user:pass@host:port</param>
        /// <param name="url">Preferred alternative URL for onionoo relay list. Could be used multiple times.</param>
        /// <param name="p">Scan for relays running on specified port number. Could be used multiple times.</param>
        /// <param name="browserLocation">Tor browser executable location</param>
        /// <param name="notInstallBridges">Not install bridges into Tor browser</param>
        /// <param name="notStartBrowser">Not launch browser after scanning</param>
        /// <param name="useOutdated">Use already existing outdated relay info</param>
        public static void Main(uint n=50, uint g=3, string? c=null, uint timeout=500, string? o=null, bool torrc=false, string? proxy=null, string[]? url=null, uint[]? p=null, string? browserLocation=null, bool notInstallBridges=false, bool notStartBrowser=false, bool useOutdated=false)
        {
            Goal = g;
            string relayInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relay-info.json");

            Console.Title = "Attemping to retrieve relay information";

            if (proxy != null) Utils.ReinitHttpClient(proxy);
            if (url != null) mirrors.InsertRange(0, url);


            TimeSpan timeDifference;
            if (RelayDistributor.TryInitialize(relayInfoPath) != null)
            {
                timeDifference = RelayDistributor.Instance.GetRelayPublishTimeDifference();
                if (timeDifference.TotalDays > 1)
                {
                    Console.WriteLine("warn: found already existing relay information, but it was outdated by {0} hours", (int)timeDifference.TotalHours);
                    if (!useOutdated) RelayDistributor.Reset();
                }
            }

            RelayDistributor distributor;
            while (RelayDistributor.Instance == null)
            {
                string mirror;
                for (int i = 0; i < mirrors.Count; i++)
                {
                    mirror = mirrors[i];
                    try
                    {
                        Console.Title = "Attemping to retrieve relay information [" + i.ToString() + "/" + mirrors.Count.ToString() + "]";
                        File.Delete(relayInfoPath);
                        Utils.DownloadToFile(mirror, relayInfoPath);
                        distributor = RelayDistributor.Initialize(relayInfoPath);
                        Console.WriteLine("done: download {0} - {1}", i, distributor.RelayInfo.Relays.Length);
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("fail: download {0} - {1}", i, e.Message);
                    }
                }
            }

            timeDifference = RelayDistributor.Instance.GetRelayPublishTimeDifference();
            if (timeDifference.TotalDays > 1)
            {
                Console.WriteLine("warn: retrieved relay information is outdated by {0} hours", (int)timeDifference.TotalHours);
            }

            Relay[] relaysToScan = RelayDistributor.Instance.RelayInfo.Relays;
            List<string> excludeCountries = new();
            List<string> includeCountries = new();
            if (c != null)
            {
                foreach(string country in c.Split(","))
                {
                    if (country.StartsWith("-")) excludeCountries.Add(country[1..]);
                    else includeCountries.Add(country);
                }
                relaysToScan = RelayDistributor.Instance.GetRelaysFromCountries(includeCountries).ExcludeCountries(excludeCountries).ToArray();
            }

            RelayScanner.OnNewWorkingRelay += RelayScanner_OnNewWorkingRelay;
            RelayScanner.OnScanEnded += RelayScanner_OnScanEnded;

            Console.CancelKeyPress += Console_CancelKeyPress;

            RelayScanner.StartScan((int)timeout, (int)n, relaysToScan, p?.ToList().ConvertAll(x => (int)x).ToArray());
            Console.WriteLine("done: scan started");
            Console.Title = "Scan started";
            RelayScanner.WaitForEnd();

            Console.Title = "Scan ended: " + WorkingRelayStrings.Count;
            Console.WriteLine("done: scan ended - {0}", WorkingRelayStrings.Count);

            if (o != null) File.WriteAllLines(o, torrc ? WorkingRelayStrings.Select(x => "Bridge " + x).Append("UseBridges 1").Reverse() : WorkingRelayStrings); // We'll try to keep UseBridges 1 as a first string

            if (browserLocation == null && (!notInstallBridges || !notStartBrowser))
            {
                Console.WriteLine("warn: trying to use auto-detected tor browser location");
                browserLocation = GetBrowserLocation();
            }

            if (!notInstallBridges) InstallBridges(Path.GetFullPath(Path.Combine(browserLocation, "TorBrowser\\Data\\Browser\\profile.default\\prefs.js")));

            if (!notStartBrowser) StartBrowser(Path.GetFullPath(browserLocation));
        }

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            if (RelayScanner.Scanning) RelayScanner.StopScan();
        }

        private static void RelayScanner_OnScanEnded(object? sender, EventArgs e)
        {
            RelayScanner.OnNewWorkingRelay -= RelayScanner_OnNewWorkingRelay;
            RelayScanner.OnScanEnded -= RelayScanner_OnScanEnded;
        }

        private static void RelayScanner_OnNewWorkingRelay(object? sender, OnNewWorkingRelayEventArgs e)
        {
            WorkingRelayStrings.Add(e.Relay);
            Console.WriteLine(e.Relay);
            Console.Title = "Scan in progress: " + WorkingRelayStrings.Count.ToString() + " found";
            if (WorkingRelayStrings.Count >= Goal) RelayScanner.StopScan();
        }

        private static string GetBrowserLocation()
        {
            string torPathAdd = "Tor Browser\\Browser";
            string location;

            List<string> searchFolders = new()
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            };

            searchFolders = searchFolders.Select(x => Path.Combine(x, torPathAdd)).ToList();
            location = searchFolders[0]; // If nothing works, we'll just keep it as desktop/Tor Browser/Browser, even if it doesnt exist

            foreach (string folder in searchFolders)
            {
                if (Directory.Exists(folder))
                {
                    Console.WriteLine("info: found tor browser directory in special folders");
                    location = Path.GetFullPath(folder);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string keyName = "SOFTWARE\\Tor Project\\Firefox\\Launcher";
                RegistryKey?[] subKeys = new RegistryKey?[]
                {
                    Registry.CurrentUser.OpenSubKey(keyName),
                    Registry.LocalMachine.OpenSubKey(keyName),
                };

                try
                {
                    foreach (RegistryKey? regKey in subKeys)
                    {
                        if (regKey == null) continue;

                        foreach (string name in regKey.GetValueNames())
                        {
                            FileInfo info;
                            if (name.Split("|")[1] == "Browser")
                            {
                                info = new FileInfo(name.Split("|")[0]);
                                if (info != null && info.Directory != null)
                                {
                                    Console.WriteLine("info: found tor browser location in registry");
                                    location = info.Directory.FullName;
                                }
                            }
                        }
                    }
                }
                catch (Exception) 
                {
                }
                finally
                {
                    subKeys.ToList().ForEach(subKey => subKey?.Dispose());
                }
            }

            return location;
        }

        private static void InstallBridges(string file)
        {
            try
            {
                List<string> contents = File.ReadAllLines(file).ToList();

                contents.RemoveAll(x => x.Contains("torbrowser.settings.bridges."));

                if (!contents.Contains("user_pref(\"torbrowser.settings.enabled\", true);"))
                {
                    contents.Remove("user_pref(\"torbrowser.settings.enabled\", false);");
                    contents.Add("user_pref(\"torbrowser.settings.enabled\", true);");
                }

                contents.Add("user_pref(\"torbrowser.settings.bridges.enabled\", true);");
                contents.Add("user_pref(\"torbrowser.settings.bridges.source\", 2);");
                contents.Add("user_pref(\"torbrowser.settings.bridges.builtin_type\", \"\");");

                for (int i = 0; i < WorkingRelayStrings.Count; i++) 
                {
                    contents.Add(string.Format("user_pref(" + '"' + "torbrowser.settings.bridges.bridge_strings.{0}" + '"' + ", {1});", i, '"' + WorkingRelayStrings[i] + '"'));
                }

                File.WriteAllLines(file, contents);
                Console.WriteLine("done: install bridges");
            }
            catch(Exception e)
            {
                Console.WriteLine("fail: install bridges - {0}", e.Message);
            }
        }

        private static void StartBrowser(string path)
        {
            string args;
            string fileName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                fileName = Path.Combine(path, "start-tor-browser");
                args = "--detach";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = Path.Combine(path, "firefox.exe");
                args = "";
            }
            else
            {
                Console.WriteLine("fail: browser start - OS unsupported");
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = fileName,
                    Arguments = args,
                    //UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                };

                Process process = new()
                {
                    StartInfo = startInfo,
                };

                process.Start();
                Console.WriteLine("done: browser start");
            }
            catch (Exception e)
            {
                Console.WriteLine("fail: browser start - {0}", e.Message);
            }
        }
    }
}
