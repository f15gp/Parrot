//通信ログとデバッグ用のログ？ないと実用的じゃねぇなぁ
using System;
using System.Net;
using System.Threading;

namespace Tester
{
    class App
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // Parrotと接続
            var tcp = new ConnectTcpLib.Client(new IPEndPoint(IPAddress.Parse("192.168.42.1"), 44444));
            var response = tcp.Handshake();
            response.Wait();
            if (response.Result.Status != -1)
            {
                Console.WriteLine("接続");

                using (var cts = new CancellationTokenSource())
                {
                    var udp = new ConnectUdpLib.Client();
                    Console.WriteLine($"C2D:{response.Result.C2DPort} UPDATE:{response.Result.C2DUpdatePort} USER:{response.Result.C2DUserPort}");
                    udp.UdpSendPort = response.Result.C2DPort;
                    udp.Connect(cts);
                    Thread.Sleep(5000);
                    udp.Discconect();
                }

                Console.WriteLine("UDP切断");
            }
            else
            {
                Console.WriteLine("接続失敗");
            }
        }
    }
}
