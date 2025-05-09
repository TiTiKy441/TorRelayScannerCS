﻿using System.Net;
using Tor_relay_scanner_CS.Relays;

namespace Tor_relay_scanner_CS
{
    public static class Utils
    {

        public readonly static Random Random = new(Convert.ToInt32(DateTime.Now.ToString("FFFFFFF")));

        private static HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(5000),
        };

        public static void Shuffle<T>(this Random rng, List<T> array)
        {
            int n = array.Count;
            while (n > 1)
            {
                int k = rng.Next(n--);
                (array[k], array[n]) = (array[n], array[k]);
            }
        }

        public static Relay? FirstOrNull(this IEnumerable<Relay> relays)
        {
            if (!relays.Any()) return null;
            return relays.First();
        }

        public static IEnumerable<Relay> ExcludeCountry(this IEnumerable<Relay> relays, string country)
        {
            return relays.Where(x => x.Country != country);
        }

        public static IEnumerable<Relay> ExcludeCountries(this IEnumerable<Relay> relays, IEnumerable<string> countries)
        {
            if (!countries.Any()) return relays;
            return relays.Where(x => !countries.Contains(x.Country));
        }

        public static string Download(string url)
        {
            return _httpClient.Send(new HttpRequestMessage(HttpMethod.Get, url)).Content.ReadAsStringAsync().Result;
        }

        public static void ReinitHttpClient(string? proxy = null)
        {
            _httpClient.Dispose();
            if (proxy == null)
            {
                _httpClient = new HttpClient()
                {
                    Timeout = _httpClient.Timeout,
                };
                return;
            }
            Uri uri = new(proxy);
            string[] creds = uri.UserInfo.Split(':' , 2);
            WebProxy webProxy = new(uri);
            if (proxy.Contains('@')) 
            {
                webProxy.Credentials = new NetworkCredential(creds[0], creds[1]);
                webProxy.UseDefaultCredentials = false;
            }
            HttpClientHandler handler = new()
            {
                Proxy = webProxy,
                UseProxy = true,
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = _httpClient.Timeout,
            };
        }

        public static void DownloadToFile(string url, string fileName)
        {
            using (Stream s = _httpClient.GetStreamAsync(url).Result)
            {
                using (FileStream fs = new(fileName, FileMode.OpenOrCreate))
                {
                    s.CopyTo(fs);
                }
            }
        }

        public async static Task DownloadToFileAsync(string url, string fileName)
        {
            using Stream s = await _httpClient.GetStreamAsync(url);
            using FileStream fs = new(fileName, FileMode.OpenOrCreate);
            await s.CopyToAsync(fs);
        }
    }
}
