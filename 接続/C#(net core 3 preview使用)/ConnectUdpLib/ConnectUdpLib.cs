using System;
using System.Threading;

using System.Net;
using System.Net.Sockets;

using System.Collections.Generic;

/// <summary>
/// UDP接続
/// </summary>
namespace ConnectUdpLib
{
    /// <summary>
    /// UDP接続(クライアント)
    /// </summary>
    public class Client
    {
        /// <summary>ローカルポート番号</summary>
        private static readonly int _LocalPort = 43210;
        /// <summary>リモートポート番号</summary>
        private static readonly int _RemotePort = 43210;
        /// <summary>Parrotからのデータを待ち受けるスレッド</summary>
        private Thread _RecvThread = null;
        /// <summary>スレッド実行キャンセル用</summary>
        private CancellationTokenSource _Cancel = null;
        /// <summary>UDP送信ポート番号(c2d_port)</summary>
        public int UdpSendPort { get; set; }

        /// <summary>
        /// UDP接続でParrotに接続する
        /// </summary>
        public void Connect(CancellationTokenSource cancel)
        {
            if (_RecvThread == null)
            {
                _Cancel = cancel;
                _RecvThread = new Thread(new ParameterizedThreadStart(RecvThreadProc));
                _RecvThread.Start(_Cancel.Token);
            }
        }

        /// <summary>
        /// ParrotからUDPを切断する
        /// </summary>
        public void Discconect()
        {
            if (_RecvThread != null)
            {
                _Cancel.Cancel();
                _RecvThread.Join();
                _RecvThread = null;
            }
        }

        /// <summary>
        /// 受信スレッド本体
        /// </summary>
        /// <param name="args">スレッド引数</param>
        public void RecvThreadProc(object args)
        {
            var local = new IPEndPoint(IPAddress.Any, _LocalPort);
            var remote = new IPEndPoint(IPAddress.Any, _RemotePort);
            var udpReceive = new UdpClient(local);
            var cancel = (CancellationToken)args;

            // 無限待ち
            for (;;)
            {
                // 切断するならスレッドを終了する
                if (cancel.IsCancellationRequested)
                {
                    break;
                }

                byte[] receiveData = udpReceive.Receive(ref remote);
#if true                
                string s = "";
                foreach (var b in receiveData)
                {
                    s += b.ToString("X2") + ",";
                }
                Console.WriteLine(s);
#endif
                HandleData(receiveData);
            }

            udpReceive.Dispose();
            udpReceive = null;
        }

        /// <summary>
        /// 受信したデータを取り扱う
        /// </summary>
        /// <param name="receive">受信データ</param>
        private void HandleData(byte[] receive)
        {
            int index = 0;

            for (;;)
            {
                var pack = new ReceiveData.Data();

                // パケットの取り出し
                pack.DataType = receive[index + 0];
                pack.BufferID = receive[index + 1];
                pack.PacketSequenceID = receive[index + 2];
                pack.PacketSize = BitConverter.ToInt32(receive, (index + 3));
                pack.ReceiveData = new byte[pack.PacketSize - 7];
                Array.Copy(receive, (index + 7), pack.ReceiveData, 0, (pack.PacketSize - 7));

                // 内容による対応
                HandleFrame(pack);

                // 複数パケットを受信していたら次のパケットを取り出す
                if (receive.Length == (index + pack.PacketSize))
                {
                    Console.WriteLine("1パケット\n");
                    break;
                }
                else
                {
                    Console.WriteLine($"複数パケット Index:{index} pack:{pack.PacketSize}");
                    index += pack.PacketSize;
                }
            }
        }

        private void HandleFrame(ReceiveData.Data recvData)
        {
            if (recvData.BufferID == ReceiveData.Constants.BufferIDs["PING"])
            {
                SendPong(recvData);
            }
        }

        private void SendPong(ReceiveData.Data recvData)
        {
            ReceiveData.Constants.SequenceCounter["PONG"] = (byte)((ReceiveData.Constants.SequenceCounter["PONG"] + 1) % 256);

            byte[] sendData = new byte[recvData.ReceiveData.Length + 7];
            sendData[0] = ReceiveData.Constants.DataTypesByName["DATA_NO_ACK"];
            sendData[1] = ReceiveData.Constants.BufferIDs["PONG"];
            sendData[2] = ReceiveData.Constants.SequenceCounter["PONG"];
            var temp = BitConverter.GetBytes(recvData.ReceiveData.Length + 7);
            Array.Copy(temp, 0, sendData, 3, temp.Length);
            //Console.WriteLine($"SendPong() temp:{temp.Length} sendData:{sendData.Length}");
            Array.Copy(recvData.ReceiveData, 0, sendData, 7, recvData.ReceiveData.Length);
            //Console.WriteLine($"SendPong() RD:{recvData.ReceiveData.Length} PSIZE:{recvData.PacketSize}");

#if true
            string s = "";
            foreach (var b in sendData)
            {
                s += b.ToString("X2") + ",";
            }
            s += '\n';
            Console.WriteLine($"SendPong() {s}");
#endif

            var parrot = new IPEndPoint(IPAddress.Parse("192.168.42.1"), UdpSendPort);
            //Console.WriteLine($"SendPong() SPORT:{UdpSendPort}");
            using (var udpSend = new UdpClient())
            {
                //udpSend.Send(sendData, sendData.Length);
                udpSend.Send(sendData, sendData.Length, parrot);    // これでOK
                //udpSend.Send(sendData, sendData.Length, "192.168.42.1", UdpSendPort);
            }
        }
    }
}

/// <summary>
/// 受信データ
/// </summary>
namespace ReceiveData
{
    public class Constants
    {
        public static readonly Dictionary<string, byte> BufferIDs = new Dictionary<string, byte> {
            { "PING", 0 },                      // ParrotからのPing
            { "PONG", 1 },                      // Pingの応答
            { "SEND_NO_ACK", 10 },              // not-ack commandsandsensors (piloting and camera rotations)
            { "SEND_WITH_ACK", 11},             // ack commandsandsensors (all piloting commandsandsensors)
            { "SEND_HIGH_PRIORITY", 12},        // emergency commandsandsensors
            { "VIDEO_ACK", 13},                 // ack for video
            { "ACK_DRONE_DATA", 127},           // drone data that needs an ack
            { "NO_ACK_DRONE_DATA", 126},        // data from drone (including battery and others), no ack
            { "VIDEO_DATA", 125 },              // video data
            { "ACK_FROM_SEND_WITH_ACK", 139 }   // 128 + buffer id for 'SEND_WITH_ACK' is 139
        };

        public static readonly Dictionary<string, byte> DataTypesByName = new Dictionary<string, byte> {
            { "ACK", 1 },
            { "DATA_NO_ACK", 2 },
            { "LOW_LATENCY_DATA", 3 },
            { "DATA_WITH_ACK", 4 },
        };

        public static Dictionary<string, byte> SequenceCounter = new Dictionary<string, byte> {
            { "PONG", 0 },
            { "SEND_NO_ACK", 0 },
            { "SEND_WITH_ACK", 0 },
            { "SEND_HIGH_PRIORITY", 0 },
            { "VIDEO_ACK", 0 },
            { "ACK_DRONE_DATA", 0 },
            { "NO_ACK_DRONE_DATA", 0 },
            { "VIDEO_DATA", 0 },
        };
    }

    /// <summary>
    /// 受信データをpackする
    /// </summary>
    public class Data
    {
        /// <summary>データ種別</summary>
        public byte DataType { get; set; }
        /// <summary>バッファID</summary>
        public byte BufferID { get; set; }
        /// <summary>パケットシーケンスID</summary>
        public byte PacketSequenceID { get; set; }
        /// <summary>パケットサイズ</summary>
        public int PacketSize { get; set; }
        /// <summary>受信データ</summary>
        public byte[] ReceiveData { get; set; }
    }
}
