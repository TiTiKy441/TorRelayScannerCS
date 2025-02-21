﻿using System.Net;
using System.Net.Sockets;

namespace Tor_relay_scanner_CS.Relays
{
    public class RelayScanner
    {

        public static bool Scanning
        {
            get
            {
                return _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;
            }
        }

        private static CancellationTokenSource _cancellationTokenSource;

        private static List<Relay> _allRelays = new();
        private static List<string> _workingRelaysFingerprints = new(); 

        public static event EventHandler<EventArgs>? OnScanEnded;
        public static event EventHandler<OnNewWorkingRelayEventArgs>? OnNewWorkingRelay;

        public static Task? StartScan(int timeout, int packetSize, Relay[]? relaysToScan=null, int[]? port=null)
        {
            if (Scanning) return null;//throw new InvalidOperationException("Scan is already in progress");

            if (relaysToScan == null) _allRelays = RelayDistributor.Instance.RelayInfo.Relays.ToList();
            else _allRelays = relaysToScan.ToList();

            Utils.Random.Shuffle(_allRelays);

            _cancellationTokenSource = new();
            return Task.Factory.StartNew(() => ScanWork(timeout, packetSize, port), _cancellationTokenSource.Token);
        }

        public static void StopScan()
        {
            if (!Scanning) return;//throw new InvalidOperationException("Scan is not in progress");
            _cancellationTokenSource.Cancel();
        }

        public static void WaitForEnd()
        {
            if (!Scanning) return;
            _cancellationTokenSource.Token.WaitHandle.WaitOne();
        }

        private static void ScanWork(int timeout, int packetSize, int[]? ports=null)
        {
            try
            {
                List<string[]> relayStrings = new();

                foreach (Relay rel in _allRelays)
                {
                    foreach (string addr in rel.Addresses)
                    {
                        if (ports != null && !ports.Contains(IPEndPoint.Parse(addr).Port)) continue;
                        relayStrings.Add(new string[2] { addr, rel.Fingerprint });
                    }
                }

                int pointer = 0;
                string[] test;

                while (pointer < relayStrings.Count && !_cancellationTokenSource.IsCancellationRequested)
                {
                    int created = 0;
                    int completed = 0;
                    while (pointer < relayStrings.Count && created < packetSize && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        test = relayStrings[pointer++];
                        created++;
                        Test(test[0], test[1], timeout).ContinueWith(t => { Interlocked.Increment(ref completed); });
                    }
                    while (completed < created)
                    {
                        _cancellationTokenSource.Token.WaitHandle.WaitOne(1);
                    }
                }
            }
            finally
            {
                if (!_cancellationTokenSource.IsCancellationRequested) _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _workingRelaysFingerprints.Clear();
                OnScanEnded?.Invoke(null, EventArgs.Empty);
            }
        }

        private static Task Test(string addr, string fingerprint, int timeout)
        {
            Socket client = new(SocketType.Stream, ProtocolType.Tcp);
            return client.ConnectAsync(IPEndPoint.Parse(addr), _cancellationTokenSource.Token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeout), _cancellationTokenSource.Token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !_cancellationTokenSource.IsCancellationRequested)
                {
                    if (!_workingRelaysFingerprints.Contains(fingerprint)) OnNewWorkingRelay?.Invoke(null, new OnNewWorkingRelayEventArgs(addr + ' ' + fingerprint));
                    _workingRelaysFingerprints.Add(fingerprint);
                }
                client.Close();
                client.Dispose();
            }, _cancellationTokenSource.Token);
        }
    }

    public class OnNewWorkingRelayEventArgs : EventArgs
    {

        public readonly string Relay;

        public OnNewWorkingRelayEventArgs(string relay) : base()
        {
            Relay = relay;
        }
    }
}

