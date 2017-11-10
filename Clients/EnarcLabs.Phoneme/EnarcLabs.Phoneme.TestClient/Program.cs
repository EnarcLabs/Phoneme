using System;
using System.Text;
using System.Threading;
using EnarcLabs.Phoneme.Binding;

namespace EnarcLabs.Phoneme.TestClient
{
   public class Program
    {
        public static void Main(string[] args)
        {
            //These keys should be in OpenSSL PEM format.
            var pupKey = Convert.FromBase64String(@"MIIBIDANBgkqhkiG9w0BAQEFAAOCAQ0AMIIBCAKCAQEAmnEzG/EPYp3abux/j1KNJzqXn8prklxU56jiQXjPO8dtNjOuGSg6THyuaVqNfQwQllxv7Tvy/0SnjTNyAckim5PiBbmDuu2DZ3WQ5oJXJT3QFE2awTVrrz7a/IbSI432P+R+rVjjc8nWHYTQdOR/C6wcZa3golgGEmhPU68SjtO8g3OmPcZ/DN+Yexdjhyh5eD6Bc3zg5SkxBymJ4NdwXM8w2Xq80gmrJEpm7LHTqUI+YNt9SOk8PXZZm38iBiLD0NPK8fWd4Xc6/yBJnkEKLbwJG89CBV9lmPkJBZn0nJ5jN8BxLyqAAKZFhRMpkUsHAVvxMvJBj6gNER34X1e6cQIBJQ==");
            var privKey = Convert.FromBase64String(@"MIIEoQIBAAKCAQEAmnEzG/EPYp3abux/j1KNJzqXn8prklxU56jiQXjPO8dtNjOuGSg6THyuaVqNfQwQllxv7Tvy/0SnjTNyAckim5PiBbmDuu2DZ3WQ5oJXJT3QFE2awTVrrz7a/IbSI432P+R+rVjjc8nWHYTQdOR/C6wcZa3golgGEmhPU68SjtO8g3OmPcZ/DN+Yexdjhyh5eD6Bc3zg5SkxBymJ4NdwXM8w2Xq80gmrJEpm7LHTqUI+YNt9SOk8PXZZm38iBiLD0NPK8fWd4Xc6/yBJnkEKLbwJG89CBV9lmPkJBZn0nJ5jN8BxLyqAAKZFhRMpkUsHAVvxMvJBj6gNER34X1e6cQIBJQKCAQApvbrJSBH++jsJOP/hi+7MVQZiibw1ZRAHQmap6UzEDGK0tAWKQjlFGsdavoca4KqJgMRN9IbdNSZdg4aYsubCQ6Te895p31Pycs03YX9WLGHATFNW0CrxF+gokyQJm/1dYFmWomb8sxdGP5JJGylBbMlnlscXHrWIbzgIx4h5oJbAJYyxfhS7GeOyb61jPdHnVKqMQWkSt9u7/ozmHVg9sH8ZbrBDEW/3HovrKqqmgPuftZE7bYeIQSPinrQJ+b7I3AFk/0mhpDe1UD65KWQuBIshrj4Qa3A2tr+9HXr5U4RZh/h7vrpWuKpn+j4f1DxaAi0t0Hm33a4gXnwHkU31AoGBANXgN0Lgpm7YMTt1xGm1Q7+KnGLMw5E8zlMR++AKTix40Br7/SK2Orl579iFjKTWLb/jBOocBGVCdD7gbvWZwvheKXpeK8vQwqYGpVh9yYrW98L6NCgpHgtD7HTdDZShiKAAq5AjVJdkx9j1BA30u6mJdw/P78nkXLIvTs3pmkgXAoGBALjcSw3/ml0aAcW96PhJGZevT0E5YOR8spdXMwbpgxb/vtkyl0EjqAaYAyJN1B3RanUVUGA3aS5Pqq+OK18UQ7y+r36eGUGhCC1ABTxAKyZGGdzxyZqfgKGKm5uGbBj6LYy4ncmoYQP0mY+o8TXAZH4q3NPTGLVPyladPl+M8J63AoGAOc3lbAVdahDqth/UOD7UCkEjWPkSQu3W5gTcSmOmbOJhwhqXcSpVD4i9XR0s7ke5VnS1OFqZZ3m+jYjECwb2bKPT2+IZrLT2VmKpOoLchmOc6JamUAsc4HM4/P15SsPfvIqPO7aFkLN0SHmSZKMCSX8ZQos544nT3SGK64s+dFkCgYEAs91CG3U8PujfIUMn3NGAr0LJpz6/I4A/D8p21kr8FmCeA8KFVCKxUoYQ4xtK7JRnlINwzFGWxUaYPBuf0iGOA8BzZnBP7NQH9Kz+LMjQCZBQg/Ir8GPXGbdLSzauXX3LdCk/k8ZsOzMtoIizVuS0zcjW2+/8XWJkAT8MQUrcR2UCgYA4dNpVTW8wCOgl037wzQjbDbGF0iLLtfGZ83vTpn0mAlvDmLfxnZLkAebjEuSCQ86WUyHGjVoVtakvWHZ90bqNNubzEW1VaMvZoIvyIxOnPFvsf8DPjVz1xhIxuUkoSM5NdB+FqZdDjFbIQjGamMTA3N+uyV4lsbEqMEMoVkD1YA==");

            // TODO: While past recorded conversations can't be read, in theory joining the mesh enables the reading of /all/ communications as they happen. Add a network of trust so at least one person who trusts you must be on the network.
            // Also, add a way to secure private messages independently.

            Console.WriteLine("Enter display name:");
            using (var client = new PhonemeClient(6969, pupKey, privKey, Console.ReadLine()))
            {
                client.Start();
                while (true)
                {
                    if (client.KnownPeers.Count >= 1)
                        break;
                    Console.WriteLine("Waiting...");
                    Thread.Sleep(1000);
                }
                Console.WriteLine("Client connected!");
                while (true)
                {
                    Console.WriteLine("Enter message:");
                    client.KnownPeers[0].SendMessage(Encoding.ASCII.GetBytes(Console.ReadLine() ?? "<null>"));
                }
            }
        }
    }
}
