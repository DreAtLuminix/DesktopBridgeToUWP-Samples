using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundTaskWinMainComSample_CS;

public static class SimplePOCSingleton
{
    public static event EventHandler<string> PrimeNumberReceived;
    public static void SendIt(string msg) => PrimeNumberReceived?.Invoke(null, msg);
}
