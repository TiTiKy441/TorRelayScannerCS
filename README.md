# TorRelayScannerCS


## C# implementation of [Tor-relay-scanner by ValdikSS](https://github.com/ValdikSS/tor-relay-scanner)
Attempts to download information about all tor relays, from official site and through mirrors embedded in the code
Attempts to ping them and find reachable ones to be used as bridges to access Tor network even in banned regions

## How to use
```
Description:
  Automatically retrieves information about tor relays and scans them for reachability

Usage:
  TorRelayScannerCS [options]

Options:
  -n <n>                                 The number of concurrent relays tested [default: 2000]
  -g <g>                                 Test until at least this number of working relays are found [default: 5]
  -c <c>                                 Include only following countries for testing, exclude by adding !. Example: se,gb,nl,de Example exclude: !nl (not netherlands) []
  --timeout <timeout>                    Socket connection timeout [default: 900]
  -o <o>                                 Output reachable relays to file []
  --torrc                                Output reachable relays in torrc format (with "Bridge" prefix) [default: False]
  --proxy <proxy>                        Set proxy for onionoo information download. Format: http://user:pass@host:port; socks5h://user:pass@host:port []
  --url <url>                            Preferred alternative URL for onionoo relay list. Could be used multiple times. []
  -p <p>                                 Scan for relays running on specified port number. Could be used multiple times. []
  --browser-location <browser-location>  Tor browser executable location []
  --not-install-bridges                  Not install bridges into Tor browser [default: False]
  --not-start-browser                    Not launch browser after scanning [default: False]
  --use-outdated                         Use already existing outdated relay info [default: False]
  --version                              Show version information
  -?, -h, --help                         Show help and usage information
```
Note: browser-location is exptected to contain an executable inside, by default on windows is usually: C:\Users\user\Desktop\Tor Browser\Browser (e.g Tor Browser\Browser in Desktop)

## Behaviour difference compared to original program
Different logging format

Browser location is not required, program tries to auto-detect it

install-bridges and start-browser turned to not-install-bridges and not-start-browser

behaviour by default install bridges and starts the browser

-c argument doesnt support preffered countries, only include or exclude
