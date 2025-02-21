using System.Data;
using System.Diagnostics;
using System.IO;
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
        /// <param name="c">Preferred country list, comma-separated. Example: se,gb,nl,de</param>
        /// <param name="timeout">Socket connection timeout</param>
        /// <param name="o">Output reachable relays to file</param>
        /// <param name="torrc">Output reachable relays in torrc format (with "Bridge" prefix)</param>
        /// <param name="proxy">Set proxy for onionoo information download. Format: http://user:pass@host:port; socks5h://user:pass@host:port</param>
        /// <param name="url">Preferred alternative URL for onionoo relay list. Could be used multiple times.</param>
        /// <param name="p">Scan for relays running on specified port number. Could be used multiple times.</param>
        /// <param name="browserLocation">Tor browser executable location for startBrowser parameter</param>
        /// <param name="notInstallBridges">Install bridges into Tor browser</param>
        /// <param name="notStartBrowser">Launch browser after scanning</param>
        /// <param name="useOutdated">Use already existing outdated relay info</param>
        public static void Main(uint n=2000, uint g=5, string? c=null, uint timeout=900, string? o=null, bool torrc=false, string? proxy=null, string[]? url=null, uint[]? p=null, string? browserLocation=null, bool notInstallBridges=false, bool notStartBrowser=false, bool useOutdated=false)
        {
            Goal = g;
            string relayInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "relay-info.json");

            Console.Title = "Attemping to retrieve relay information";

            if (proxy != null) Utils.ReinitHttpClient(proxy);
            if (url != null) mirrors.AddRange(url);


            if (RelayDistributor.TryInitialize(relayInfoPath) != null)
            {
                DateTime downloadedPublishedDate = DateTime.Parse(RelayDistributor.Instance.RelayInfo.RelaysPublishDate);
                TimeSpan downloadedDiff = DateTime.Now.Subtract(downloadedPublishedDate);
                if (downloadedDiff.TotalDays > 1)
                {
                    Console.WriteLine("warning: found already existing relay information, but it was outdated by {0} hours", (int)downloadedDiff.TotalHours);
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

            Relay[] relaysToScan = RelayDistributor.Instance.RelayInfo.Relays;
            if (c != null) relaysToScan = RelayDistributor.Instance.GetRelaysFromCountries(c.Split(","));

            DateTime publishedDate = DateTime.Parse(RelayDistributor.Instance.RelayInfo.RelaysPublishDate);
            TimeSpan diff = DateTime.Now.Subtract(publishedDate);
            if (diff.TotalDays > 1)
            {
                Console.WriteLine("warning: retrieved relay information is outdated by {0} hours", (int)diff.TotalHours);
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

            if (o != null) File.WriteAllLines(o, WorkingRelayStrings.Select((x, _) => (torrc ? "Bridge " : "") + x).Append("UseBridges 1").Reverse()); // We'll try to keep UseBridges 1 as a first string

            if (browserLocation == null && (!notInstallBridges || !notStartBrowser))
            {
                Console.WriteLine("warning: trying to use auto-detected tor browser location");
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
            string location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Tor Browser\\Browser");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string keyName = "SOFTWARE\\Tor Project\\Firefox\\Launcher";
                RegistryKey?[] subKeys = new RegistryKey?[]
                {
                    Registry.CurrentUser.OpenSubKey(keyName),
                    Registry.LocalMachine.OpenSubKey(keyName)
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
                                if (info != null && info.Directory != null) return info.Directory.FullName;
                            }
                        }
                    }
                }
                catch (Exception) {}
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
                contents.Add("user_pref(" + '"' + "torbrowser.settings.bridges.enabled" + '"' + ", true);");
                contents.Add("user_pref(" + '"' + "torbrowser.settings.bridges.source" + '"' + ", 2);");

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
            }
            catch (Exception e)
            {
                Console.WriteLine("fail: browser start - {0}", e.Message);
            }
        }
    }
}
