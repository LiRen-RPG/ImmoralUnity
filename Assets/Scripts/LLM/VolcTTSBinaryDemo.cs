// 火山引擎TTS WebSocket二进制协议 Unity版本实现
// Unity WebSocket 依赖: NativeWebSocket (推荐使用)

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
#if HAVE_NATIVEWEBSOCKET
using NativeWebSocket;
#endif

namespace Immortal.TTS
{
    [System.Serializable]
    public class VolcTTSBinaryOptions
    {
        public string appid;
        public string token;
        public string cluster = "volcano_tts";
        public string voice_type = "zh_male_M392_conversation_wvae_bigtts";
        public string uid = "uid123";
        public string text;
        public float speed_ratio = 1.0f;
        public float volume_ratio = 1.0f;
        public float pitch_ratio = 1.0f;
        public string encoding = "mp3";
        public string reqid;

        public VolcTTSBinaryOptions()
        {
            reqid = System.Guid.NewGuid().ToString();
        }
    }

    [System.Serializable]
    public class VolcTTSRequest
    {
        public App app;
        public User user;
        public Audio audio;
        public RequestInfo request;

        [System.Serializable]
        public class App
        {
            public string appid;
            public string token;
            public string cluster;
        }

        [System.Serializable]
        public class User
        {
            public string uid;
        }

        [System.Serializable]
        public class Audio
        {
            public string voice_type;
            public string encoding;
            public float speed_ratio;
            public float volume_ratio;
            public float pitch_ratio;
        }

        [System.Serializable]
        public class RequestInfo
        {
            public string reqid;
            public string text;
            public string text_type = "plain";
            public string operation;
        }
    }

    public static class VolcTTSBinaryDemo
    {
        /// <summary>
        /// 构建协议头部
        /// </summary>
        private static byte[] BuildHeader()
        {
            // version: 1, header size: 1, message type: 1, flags: 0, serialization: 1, compression: 0, reserved: 0
            // 0x11 0x10 0x10 0x00
            return new byte[] { 0x11, 0x10, 0x10, 0x00 };
        }

        /// <summary>
        /// 构建请求负载
        /// </summary>
        private static byte[] BuildRequestPayload(VolcTTSBinaryOptions opts, string operation)
        {
            var req = new VolcTTSRequest
            {
                app = new VolcTTSRequest.App
                {
                    appid = opts.appid,
                    token = opts.token,
                    cluster = opts.cluster
                },
                user = new VolcTTSRequest.User
                {
                    uid = opts.uid
                },
                audio = new VolcTTSRequest.Audio
                {
                    voice_type = opts.voice_type,
                    encoding = opts.encoding,
                    speed_ratio = opts.speed_ratio,
                    volume_ratio = opts.volume_ratio,
                    pitch_ratio = opts.pitch_ratio
                },
                request = new VolcTTSRequest.RequestInfo
                {
                    reqid = opts.reqid,
                    text = opts.text,
                    text_type = "plain",
                    operation = operation
                }
            };

            string jsonStr = JsonConvert.SerializeObject(req);
            // 使用UTF-8编码将字符串转换为字节数组
            byte[] compressed = Encoding.UTF8.GetBytes(jsonStr);
            return compressed;
        }

        /// <summary>
        /// 构建完整的客户端请求
        /// </summary>
        private static byte[] BuildFullClientRequest(byte[] payload)
        {
            byte[] header = BuildHeader();
            int payloadSize = payload.Length;
            byte[] buf = new byte[header.Length + 4 + payloadSize];

            // 设置头部
            Array.Copy(header, 0, buf, 0, header.Length);

            // 设置负载大小（4字节，大端序）
            buf[4] = (byte)((payloadSize >> 24) & 0xff);
            buf[5] = (byte)((payloadSize >> 16) & 0xff);
            buf[6] = (byte)((payloadSize >> 8) & 0xff);
            buf[7] = (byte)(payloadSize & 0xff);

            // 设置负载
            Array.Copy(payload, 0, buf, 8, payloadSize);

            return buf;
        }

        /// <summary>
        /// 火山引擎TTS二进制协议演示
        /// </summary>
        /// <param name="opts">TTS配置选项</param>
        /// <param name="onAudioChunk">音频数据块回调</param>
        /// <returns>完整的音频数据</returns>
        public static async Task<byte[]> ExecuteVolcTTSBinaryDemo(
            VolcTTSBinaryOptions opts,
            Action<byte[]> onAudioChunk = null)
        {
            string apiUrl = $"wss://{opts.token}@openspeech.bytedance.com/api/v1/tts/ws_binary";
            byte[] payload = BuildRequestPayload(opts, "submit");
            byte[] fullRequest = BuildFullClientRequest(payload);

            var tcs = new TaskCompletionSource<byte[]>();
            var audioBuffers = new List<byte[]>();

            WebSocket webSocket = new WebSocket(apiUrl);
            webSocket.OnOpen += () =>
            {
                Debug.Log("WebSocket连接已打开");
                webSocket.Send(fullRequest);
            };

            webSocket.OnMessage += (bytes) =>
            {
                try
                {
                    ParseResponse(bytes, audioBuffers, onAudioChunk, webSocket, tcs);
                }
                catch (Exception e)
                {
                    Debug.LogError($"解析响应时出错: {e.Message}");
                    tcs.SetException(e);
                }
            };

            webSocket.OnError += (e) =>
            {
                Debug.LogError($"WebSocket错误: {e}");
                tcs.SetException(new Exception($"WebSocket错误: {e}"));
            };

            webSocket.OnClose += (e) =>
            {
                Debug.Log("WebSocket连接已关闭");
            };

            // 连接WebSocket
            await webSocket.Connect();

            return await tcs.Task;
        }

        /// <summary>
        /// 解析WebSocket响应
        /// </summary>
        private static void ParseResponse(byte[] response, List<byte[]> audioBuffers, 
            Action<byte[]> onAudioChunk, WebSocket webSocket, TaskCompletionSource<byte[]> tcs)
        {
            if (response.Length < 4)
            {
                Debug.LogWarning("响应数据太短");
                return;
            }

            // 解析协议头
            byte headerSize = (byte)(response[0] & 0x0f);
            byte messageType = (byte)(response[1] >> 4);
            byte messageTypeSpecificFlags = (byte)(response[1] & 0x0f);

            byte[] payload = new byte[response.Length - headerSize * 4];
            Array.Copy(response, headerSize * 4, payload, 0, payload.Length);

            if (messageType == 0xb) // audio-only server response
            {
                if (messageTypeSpecificFlags == 0)
                {
                    // no sequence number as ACK
                    // do nothing
                }
                else
                {
                    if (payload.Length < 8)
                    {
                        Debug.LogWarning("音频响应负载太短");
                        return;
                    }

                    int sequenceNumber = (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3];
                    uint payloadSize = (uint)((payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7]);

                    if (payload.Length < 8 + payloadSize)
                    {
                        Debug.LogWarning("音频数据长度不匹配");
                        return;
                    }

                    byte[] audioData = new byte[payloadSize];
                    Array.Copy(payload, 8, audioData, 0, (int)payloadSize);
                    audioBuffers.Add(audioData);

                    onAudioChunk?.Invoke(audioData);

                    if (sequenceNumber < 0)
                    {
                        // 最后一包
                        webSocket.Close();
                        
                        // 合并所有音频数据
                        int totalLength = 0;
                        foreach (var buf in audioBuffers)
                        {
                            totalLength += buf.Length;
                        }

                        byte[] merged = new byte[totalLength];
                        int offset = 0;
                        foreach (var buf in audioBuffers)
                        {
                            Array.Copy(buf, 0, merged, offset, buf.Length);
                            offset += buf.Length;
                        }

                        tcs.SetResult(merged);
                    }
                }
            }
            else if (messageType == 0xf) // error
            {
                if (payload.Length < 8)
                {
                    tcs.SetException(new Exception("错误响应格式无效"));
                    return;
                }

                // 解析错误代码和消息大小
                uint code = (uint)((payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]);
                uint msgSize = (uint)((payload[4] << 24) | (payload[5] << 16) | (payload[6] << 8) | payload[7]);

                if (payload.Length < 8 + msgSize)
                {
                    tcs.SetException(new Exception("错误消息长度不匹配"));
                    return;
                }

                byte[] errorMsgBytes = new byte[msgSize];
                Array.Copy(payload, 8, errorMsgBytes, 0, (int)msgSize);

                // 如果使用压缩，在此处解压缩
                string errorMsg = Encoding.UTF8.GetString(errorMsgBytes);

                Debug.LogError($"错误代码: {code}");
                Debug.LogError($"错误消息大小: {msgSize} 字节");
                Debug.LogError($"错误消息: {errorMsg}");

                tcs.SetException(new Exception($"VolcTTS错误响应: {errorMsg}"));
            }
        }
    }

#if !HAVE_NATIVEWEBSOCKET
    // ---------------------------------------------------------------------------
    // Stub WebSocket 类 —— 仅在未安装 NativeWebSocket 包时编译。
    // 安装步骤：在 Packages/manifest.json 中添加
    //   "com.endel.nativewebsocket": "https://github.com/endel/NativeWebSocket.git#upm"
    // 然后在 Player Settings > Scripting Define Symbols 中添加 HAVE_NATIVEWEBSOCKET
    // ---------------------------------------------------------------------------
    internal class WebSocket
    {
        public System.Action OnOpen;
        public System.Action<byte[]> OnMessage;
        public System.Action<string> OnError;
        public System.Action<object> OnClose;

        public WebSocket(string url) { }

        public System.Threading.Tasks.Task Connect()
        {
            throw new System.NotImplementedException(
                "NativeWebSocket 包未安装。请参见 VolcTTSBinaryDemo.cs 顶部的安装说明。");
        }

        public System.Threading.Tasks.Task Send(byte[] data) { return System.Threading.Tasks.Task.CompletedTask; }

        public void Close()
        {
            throw new System.NotImplementedException(
                "NativeWebSocket 包未安装。");
        }
    }
#endif
}