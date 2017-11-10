using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EnarcLabs.Phoneme.Binding;

namespace EnarcLabs.Phoneme.TestUI
{
    public class MainUiModel : IDisposable
    {
        public ObservableCollection<PeerMessage> Messages { get; }

        public PhonemeClient Client { get; }

        public ICommand SendMessageCommand => new SendMessageCommandObject(this);

        public MainUiModel()
        {
            //var privKey = Convert.FromBase64String(@"MIICXAIBAAKBgQCC11ODfQ4yt3But3ZtBxfxoPU1kRzj+fdsrcBiBDlOLYHuzs+nr1awp8M99Xz2V3uDs4LaUdltSAlyUyYUCJayV5gjwzUKQM+2qBecbsxBwhlFau8s7j0vo88YPz26irNNmAAsBHICFB9EJRV6GuxuqToD7kfNOZ7F9MgnHaP2HwIDAQABAoGAXeGC2uXwOhPFaKvbHX/pfkavqy/kOvAwyJojYDEHrUCZ6nAaL4dv/HFjdiGe+GLtDSLQ0TXJfNAjdxSSTe2bsnCfXaTapUfp4ugtQ2FAJ9wsQXkUMtPzxq58YaOyPRKFi80RQOU/oslRUmFLhWdjhCtqX6JTsV8a3tBC19/KA4ECQQDbHKyXGsqC6E93WiZ0wKXPLD6EtLGApoSCCguq7HKfhVW1sZD4iyKwttMhdB1JhqqZx1xQrilNbNGkJf5PydejAkEAmN5WqMp+RNVQRUu6A27yHX9gELRdEkRiTN6oPA9AZtqIBxTHhX8ssDY0ovSvYIx25ECCcKLab07MGWZy1Bz/VQJBAL2pHFvPfOvDWsXc6ty0xNGHYrZMEjlh6eEGAQN6l90s9PvJL8tz5BtCpY6Xi6JRRurFfkr39hhm0TBdErzN4jECQCbeCRybd6VaszEbQu1SjR6w3yUAJtXZK0EuL4otuossLv/V6bDol90puxJfsiOTMztvp3qp/W3llAE1SibiRI0CQEjy2xNq2N1YvIRVRK08xaV54bEQAIe5fn10FlK4Gss3a3Lu8TT8zwwaqOxVP80r39YzGDwjcP6817UjJQMfBNU=");
            //var pupKey = Convert.FromBase64String(@"MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCC11ODfQ4yt3But3ZtBxfxoPU1kRzj+fdsrcBiBDlOLYHuzs+nr1awp8M99Xz2V3uDs4LaUdltSAlyUyYUCJayV5gjwzUKQM+2qBecbsxBwhlFau8s7j0vo88YPz26irNNmAAsBHICFB9EJRV6GuxuqToD7kfNOZ7F9MgnHaP2HwIDAQAB");

            var pupKey = Convert.FromBase64String(@"MIIBIDANBgkqhkiG9w0BAQEFAAOCAQ0AMIIBCAKCAQEAmnEzG/EPYp3abux/j1KNJzqXn8prklxU56jiQXjPO8dtNjOuGSg6THyuaVqNfQwQllxv7Tvy/0SnjTNyAckim5PiBbmDuu2DZ3WQ5oJXJT3QFE2awTVrrz7a/IbSI432P+R+rVjjc8nWHYTQdOR/C6wcZa3golgGEmhPU68SjtO8g3OmPcZ/DN+Yexdjhyh5eD6Bc3zg5SkxBymJ4NdwXM8w2Xq80gmrJEpm7LHTqUI+YNt9SOk8PXZZm38iBiLD0NPK8fWd4Xc6/yBJnkEKLbwJG89CBV9lmPkJBZn0nJ5jN8BxLyqAAKZFhRMpkUsHAVvxMvJBj6gNER34X1e6cQIBJQ==");
            var privKey = Convert.FromBase64String(@"MIIEoQIBAAKCAQEAmnEzG/EPYp3abux/j1KNJzqXn8prklxU56jiQXjPO8dtNjOuGSg6THyuaVqNfQwQllxv7Tvy/0SnjTNyAckim5PiBbmDuu2DZ3WQ5oJXJT3QFE2awTVrrz7a/IbSI432P+R+rVjjc8nWHYTQdOR/C6wcZa3golgGEmhPU68SjtO8g3OmPcZ/DN+Yexdjhyh5eD6Bc3zg5SkxBymJ4NdwXM8w2Xq80gmrJEpm7LHTqUI+YNt9SOk8PXZZm38iBiLD0NPK8fWd4Xc6/yBJnkEKLbwJG89CBV9lmPkJBZn0nJ5jN8BxLyqAAKZFhRMpkUsHAVvxMvJBj6gNER34X1e6cQIBJQKCAQApvbrJSBH++jsJOP/hi+7MVQZiibw1ZRAHQmap6UzEDGK0tAWKQjlFGsdavoca4KqJgMRN9IbdNSZdg4aYsubCQ6Te895p31Pycs03YX9WLGHATFNW0CrxF+gokyQJm/1dYFmWomb8sxdGP5JJGylBbMlnlscXHrWIbzgIx4h5oJbAJYyxfhS7GeOyb61jPdHnVKqMQWkSt9u7/ozmHVg9sH8ZbrBDEW/3HovrKqqmgPuftZE7bYeIQSPinrQJ+b7I3AFk/0mhpDe1UD65KWQuBIshrj4Qa3A2tr+9HXr5U4RZh/h7vrpWuKpn+j4f1DxaAi0t0Hm33a4gXnwHkU31AoGBANXgN0Lgpm7YMTt1xGm1Q7+KnGLMw5E8zlMR++AKTix40Br7/SK2Orl579iFjKTWLb/jBOocBGVCdD7gbvWZwvheKXpeK8vQwqYGpVh9yYrW98L6NCgpHgtD7HTdDZShiKAAq5AjVJdkx9j1BA30u6mJdw/P78nkXLIvTs3pmkgXAoGBALjcSw3/ml0aAcW96PhJGZevT0E5YOR8spdXMwbpgxb/vtkyl0EjqAaYAyJN1B3RanUVUGA3aS5Pqq+OK18UQ7y+r36eGUGhCC1ABTxAKyZGGdzxyZqfgKGKm5uGbBj6LYy4ncmoYQP0mY+o8TXAZH4q3NPTGLVPyladPl+M8J63AoGAOc3lbAVdahDqth/UOD7UCkEjWPkSQu3W5gTcSmOmbOJhwhqXcSpVD4i9XR0s7ke5VnS1OFqZZ3m+jYjECwb2bKPT2+IZrLT2VmKpOoLchmOc6JamUAsc4HM4/P15SsPfvIqPO7aFkLN0SHmSZKMCSX8ZQos544nT3SGK64s+dFkCgYEAs91CG3U8PujfIUMn3NGAr0LJpz6/I4A/D8p21kr8FmCeA8KFVCKxUoYQ4xtK7JRnlINwzFGWxUaYPBuf0iGOA8BzZnBP7NQH9Kz+LMjQCZBQg/Ir8GPXGbdLSzauXX3LdCk/k8ZsOzMtoIizVuS0zcjW2+/8XWJkAT8MQUrcR2UCgYA4dNpVTW8wCOgl037wzQjbDbGF0iLLtfGZ83vTpn0mAlvDmLfxnZLkAebjEuSCQ86WUyHGjVoVtakvWHZ90bqNNubzEW1VaMvZoIvyIxOnPFvsf8DPjVz1xhIxuUkoSM5NdB+FqZdDjFbIQjGamMTA3N+uyV4lsbEqMEMoVkD1YA==");

            Messages = new ObservableCollection<PeerMessage>();
            Client = new PhonemeClient(6969, pupKey, privKey, new FlatFileVerifier(@"C:\Users\atrewin\Desktop\trustedKeys"), Dns.GetHostName());
            Client.Start();
            Client.PeerJoin += ClientOnPeerJoin;
        }

        private void ClientOnPeerJoin(PhonemePeer peer)
        {
            peer.MessageRecieved += PeerOnMessageRecieved;
        }

        private void PeerOnMessageRecieved(PhonemePeer source, byte[] data)
        {
            Application.Current.Dispatcher.Invoke(() => Messages.Add(new PeerMessage(source, Encoding.ASCII.GetString(data))));
        }

        public void Dispose()
        {
            Client.Dispose();
        }

        private class SendMessageCommandObject : ICommand
        {
            private readonly MainUiModel _model;

            public SendMessageCommandObject(MainUiModel model)
            {
                _model = model;
            }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
                var str = parameter as string;
                if(string.IsNullOrWhiteSpace(str))
                    return;

                foreach (var peer in _model.Client.KnownPeers)
                    peer.SendMessage(Encoding.ASCII.GetBytes(str));

                _model.Messages.Add(new PeerMessage(null, str));
            }

            public event EventHandler CanExecuteChanged;
        }

        private class FlatFileVerifier : ITrustVerifier
        {
            private readonly string _filePath;

            public FlatFileVerifier(string filePath)
            {
                _filePath = filePath;
            }

            public bool VerifyTrust(byte[] publicKey)
            {
                using (var rdr = File.OpenText(_filePath))
                {
                    string line;
                    while ((line = rdr.ReadLine()) != null)
                    {
                        var data = Convert.FromBase64String(line);
                        if (CompareBytes(data, publicKey))
                            return true;
                    }
                }

                return false;
            }

            [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
            private static extern int memcmp(byte[] b1, byte[] b2, long count);

            public static bool CompareBytes(byte[] a, byte[] b) => !(a == null || b == null || a.Length != b.Length) && memcmp(a, b, a.Length) == 0;
        }
    }

    public struct PeerMessage
    {
        public PhonemePeer Peer { get; }
        public string MessageText { get; }

        public PeerMessage(PhonemePeer peer, string messageText)
        {
            Peer = peer;
            MessageText = messageText;
        }
    }
}
