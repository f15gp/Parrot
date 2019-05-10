using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Linq;

using System.Diagnostics;

/// <summary>
/// JSON用ユーティリティ
/// </summary>
namespace JsonUtility
{
    public static class Json
    {
        /// <summary>
        /// 任意のオブジェクトをJSONにシリアライズする
        /// </summary>
        /// <param name="obj">シリアライズ オブジェクト</param>
        /// <returns>文字列(UTF8)に変換したJSON</returns>
        public static string Serialize(object obj)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    var ser = new DataContractJsonSerializer(obj.GetType());
                    ser.WriteObject(stream, obj);
                    return System.Text.Encoding.UTF8.GetString(stream.ToArray());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// JSONを任意のオブジェクトへデシリアライズする
        /// </summary>
        /// <param name="json">デシリアライズするJSON</param>
        /// <typeparam name="T">任意のオブジェクト型</typeparam>
        /// <returns>デシリアライズしたJSONを任意のオブジェクトに変換したもの</returns>
        public static T Deserialize<T>(string json)
        {
            // 空白があるとデシリアライズに失敗するので、最初に空白を除去する。
            // また、GetStringは\0をそのまま変換するので削除しておく
            var trimmed = string.Concat(json.Where(x => !Char.IsWhiteSpace(x)));
            trimmed = trimmed.TrimEnd('\0');

            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trimmed)))
            {
                var deserializer = new DataContractJsonSerializer(typeof(T));
                return (T)deserializer.ReadObject(stream);
            }
        }
    }
}

/// <summary>
/// JSON用データ
/// </summary>
namespace JsonData
{
    /// <summary>
    /// TCP接続時Parrotへ送るJSONデータ
    /// </summary>
    [DataContract]
    public class ParrotHandshakeData
    {
        [DataMember(Name="d2c_port")]
        public int D2CPort { get; set; }

        [DataMember(Name="controller_type")]
        public string ControllerType { get; set; }

        [DataMember(Name="controller_name")]
        public string ControllerName { get; set; }

        [DataMember(Name="arstream2_client_stream_port")]
        public int StreamPort { get; set; }

        [DataMember(Name="arstream2_client_control_port")]
        public int StreamControlPort { get; set; }
    }

    /// <summary>
    /// TCP接続時Parrotから送られるJSONデータ
    /// </summary>
    [DataContract]
    public class ParrotResponse
    {
        [DataMember(Name="status")]
        public int Status { get; set; }

        [DataMember(Name="c2d_port")]
        public int C2DPort { get; set; }

        [DataMember(Name="c2d_update_port")]
        public int C2DUpdatePort { get; set; }

        [DataMember(Name="c2d_user_port")]
        public int C2DUserPort { get; set; }

        [DataMember(Name="qos_mode")]
        public int QosMode { get; set; }

        [DataMember(Name="arstream2_server_stream_port")]
        public int StreamPort { get; set; }

        [DataMember(Name="arstream2_server_control_port")]
        public int StreamControlPort { get; set; }
    }
}

/// <summary>
/// TCP接続
/// </summary>
namespace ConnectTcpLib
{
    /// <summary>
    /// TCP接続(クライアント) 送信すると必ず応答を待つ
    /// </summary>
    public class Client
    {
        /// <summary>接続先エンドポイント(IPアドレス＆ポート番号)</summary>
        private readonly IPEndPoint _EndPoint;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="endPoint">接続先エンドポイント</param>
        public Client(IPEndPoint endPoint)
        {
            _EndPoint = endPoint;
        }

        /// <summary>
        /// Parrotと接続するためのハンドシェイクを行う
        /// </summary>
        /// <returns>Parrot送信データ(失敗時はStatusに-1を設定する)</returns>
        /// <remarks>戻り値は受け取ったデータをシャローコピーしたものです。</remarks>
        public async Task<JsonData.ParrotResponse> Handshake()
        {
            var ret = new JsonData.ParrotResponse(){ Status = -1 };
            try
            {
                using (var client = new TcpClient())
                {
                    // Parrotに接続
                    await client.ConnectAsync(_EndPoint.Address, _EndPoint.Port);

                    // Parrotとハンドシェイク
                    using (var stream = client.GetStream())
                    {
                        // 送信用JSONの変換
                        var json = new JsonData.ParrotHandshakeData()
                        {
                            D2CPort = 43210,
                            ControllerType = "computer",
                            ControllerName = "pyparrot",
                            StreamPort = 55004,
                            StreamControlPort = 55005
                        };
                        var sendJsonString = JsonUtility.Json.Serialize(json);
                        var send = System.Text.Encoding.UTF8.GetBytes(sendJsonString);

                        // Parrotに送信
                        await stream.WriteAsync(send, 0, send.Length);

                        // Parrotからの応答を待つ
                        var recv = new Byte[4096];  // PyParrotがこうやって定義してた
                        await stream.ReadAsync(recv, 0, recv.Length);
                        var response = System.Text.Encoding.UTF8.GetString(recv);

                        // 応答内容の確認
                        var deserialize = JsonUtility.Json.Deserialize<JsonData.ParrotResponse>(response);
                        if (deserialize.Status != 0)
                        {
                            throw(new InvalidOperationException($"ParrotがStatus:{deserialize.Status}を返した"));
                        }
                        ret = deserialize;
                    }
                }
            }
            catch (Exception ex)
            {
                // デバッグ用
                Console.WriteLine($"例外: {ex.Message}");
            }

            return ret;
        }
    }
}
