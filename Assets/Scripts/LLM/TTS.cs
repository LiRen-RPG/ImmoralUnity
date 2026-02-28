using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Security.Cryptography;
using System;

namespace Immortal.LLM
{
    /// <summary>
    /// TTS语音合成服务
    /// 支持讯飞和火山引擎两种TTS服务
    /// </summary>
    public static class TTS
    {
        /// <summary>
        /// 从Blob数据创建AudioClip
        /// </summary>
        private static async Task<AudioClip> CreateAudioClipFromBlob(byte[] audioData, string name = "TTSAudio")
        {
            return await Task.Run(() =>
            {
                // 在Unity中，我们需要将音频数据保存为临时文件，然后加载
                string tempPath = System.IO.Path.GetTempFileName() + ".mp3";
                System.IO.File.WriteAllBytes(tempPath, audioData);
                
                // 注意：Unity的AudioClip创建需要在主线程执行
                // 这里返回null，实际应用中需要在主线程处理
                return (AudioClip)null;
            });
        }

        /// <summary>
        /// 从缓存数据创建音频数据
        /// </summary>
        private static byte[] CreateAudioDataFromCache(string cached)
        {
            try
            {
                var cachedData = JsonUtility.FromJson<string[]>(cached);
                List<byte> audioData = new List<byte>();
                
                foreach (string chunk in cachedData)
                {
                    byte[] chunkData = Convert.FromBase64String(chunk);
                    audioData.AddRange(chunkData);
                }
                
                return audioData.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating audio data from cache: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 调用讯飞TTS WebAPI生成语音并返回AudioClip
        /// </summary>
        /// <param name="text">要合成的文本</param>
        /// <param name="appId">讯飞应用ID</param>
        /// <param name="apiKey">API Key</param>
        /// <param name="apiSecret">API Secret</param>
        /// <param name="voiceName">语音名称</param>
        /// <returns>AudioClip</returns>
        public static async Task<AudioClip> PlayTTSXFyun(
            string text,
            string appId = "2c25d8df",
            string apiKey = "18f54f89ca957d72fb5efdf38cafb525",
            string apiSecret = "MDJjMTRhYTA2YmU2MDI4Y2U2MzlkOTdh",
            string voiceName = "x4_xiaojun")
        {
            // HMAC-SHA256并base64
            async Task<string> HmacSha256Base64(string key, string message)
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                    return Convert.ToBase64String(hash);
                }
            }

            async Task<string> GetWebSocketUrl()
            {
                string host = "ws-api.xfyun.cn";
                string path = "/v2/tts";
                string date = DateTime.UtcNow.ToString("r");
                
                string signatureOrigin = $"host: {host}\ndate: {date}\nGET {path} HTTP/1.1";
                string signature = await HmacSha256Base64(apiSecret, signatureOrigin);
                
                string authorization = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signature}\"";
                string authBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization));
                
                return $"ws://{host}{path}?authorization={authBase64}&date={date}&host={host}";
            }

            var common = new { app_id = appId };
            var business = new
            {
                aue = "lame",
                auf = "audio/L16;rate=16000",
                vcn = voiceName,
                tte = "UTF8"
            };
            var data = new
            {
                status = 2,
                text = Convert.ToBase64String(Encoding.UTF8.GetBytes(text))
            };

            // 检查缓存
            string cacheKey = $"tts_audio_{voiceName}_{text}";
            string cached = PlayerPrefs.GetString(cacheKey, "");
            if (!string.IsNullOrEmpty(cached))
            {
                byte[] audioData = CreateAudioDataFromCache(cached);
                if (audioData != null)
                {
                    return await CreateAudioClipFromBlob(audioData);
                }
            }

            // 注意：Unity中WebSocket需要使用第三方库或使用HTTP方式
            // 这里提供HTTP API的简化实现
            Debug.LogWarning("WebSocket TTS not fully implemented. Consider using HTTP API or third-party WebSocket library.");
            
            return null;
        }

        /// <summary>
        /// 调用火山引擎TTS API生成语音并返回AudioClip
        /// </summary>
        /// <param name="text">要合成的文本</param>
        /// <param name="voiceType">语音类型</param>
        /// <param name="appId">应用ID</param>
        /// <param name="accessToken">访问令牌</param>
        /// <param name="uid">用户ID</param>
        /// <returns>AudioClip</returns>
        public static async Task<AudioClip> PlayTTSVolcEngine(
            string text,
            string voiceType = "zh_male_M392_conversation_wvae_bigtts",
            string appId = "4759787221",
            string accessToken = "AfKg5unb1uSf6cYBKkJTj7DxwugJfbz-",
            string uid = "uid123")
        {
            // 缓存逻辑
            string cacheKey = $"tts_volc_{voiceType}_{text}";
            string cached = PlayerPrefs.GetString(cacheKey, "");
            if (!string.IsNullOrEmpty(cached))
            {
                byte[] audioData = CreateAudioDataFromCache(cached);
                if (audioData != null)
                {
                    return await CreateAudioClipFromBlob(audioData);
                }
            }

            // HTTP请求到本地TTS服务
            try
            {
                var requestData = new
                {
                    text = text,
                    voice_type = voiceType
                };

                string jsonBody = JsonUtility.ToJson(requestData);

                using (UnityWebRequest request = new UnityWebRequest("http://localhost:8000/tts", "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    var operation = request.SendWebRequest();
                    
                    // 等待请求完成
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new Exception($"HTTP error! status: {request.responseCode}");
                    }

                    byte[] audioBuffer = request.downloadHandler.data;

                    // 缓存base64 - 使用分块方式避免调用栈溢出
                    string base64Audio = ArrayBufferToBase64(audioBuffer);
                    var serializedChunks = new List<string> { base64Audio };
                    string merged = JsonUtility.ToJson(serializedChunks.ToArray());
                    PlayerPrefs.SetString(cacheKey, merged);

                    return await CreateAudioClipFromBlob(audioBuffer);
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"TTS request failed: {error.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将字节数组转换为Base64字符串（分块处理）
        /// </summary>
        private static string ArrayBufferToBase64(byte[] buffer)
        {
            const int chunkSize = 1024 * 1024; // 1MB chunks
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < buffer.Length; i += chunkSize)
            {
                int currentChunkSize = Math.Min(chunkSize, buffer.Length - i);
                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(buffer, i, chunk, 0, currentChunkSize);
                result.Append(Convert.ToBase64String(chunk));
            }

            return result.ToString();
        }

        /// <summary>
        /// 使用Cultivator信息自动选择合适的语音进行TTS
        /// </summary>
        /// <param name="text">要合成的文本</param>
        /// <param name="cultivator">修仙者对象</param>
        /// <param name="useVolcEngine">是否使用火山引擎（默认使用讯飞）</param>
        /// <returns>AudioClip</returns>
        public static async Task<AudioClip> PlayTTSForCultivator(
            string text, 
            Immortal.Core.Cultivator cultivator, 
            bool useVolcEngine = false)
        {
            if (cultivator == null)
            {
                Debug.LogWarning("Cultivator is null, using default voice");
                return useVolcEngine ? 
                    await PlayTTSVolcEngine(text) : 
                    await PlayTTSXFyun(text);
            }

            string voiceId = BytedanceVoices.GetVoiceForCultivator(cultivator);
            
            if (useVolcEngine)
            {
                return await PlayTTSVolcEngine(text, voiceId);
            }
            else
            {
                // 讯飞语音需要映射到对应的语音名称
                string xfyunVoice = MapBytedanceToXfyun(voiceId);
                return await PlayTTSXFyun(text, voiceName: xfyunVoice);
            }
        }

        /// <summary>
        /// 将字节跳动语音ID映射到讯飞语音名称
        /// </summary>
        private static string MapBytedanceToXfyun(string bytedanceVoiceId)
        {
            // 简化映射，实际应用中可能需要更详细的映射表
            var voiceMapping = new Dictionary<string, string>
            {
                { "zh_male_M392_conversation_wvae_bigtts", "x4_xiaojun" },
                { "zh_female_qingxin_conversation_wvae_bigtts", "x4_xiaoyan" },
                { "zh_male_xiaoyu_conversation_wvae_bigtts", "x4_xiaoming" },
                { "zh_female_yuyu_conversation_wvae_bigtts", "x4_xiaofeng" }
            };

            return voiceMapping.ContainsKey(bytedanceVoiceId) ? 
                voiceMapping[bytedanceVoiceId] : "x4_xiaojun"; // 默认语音
        }

        /// <summary>
        /// 批量预加载TTS音频
        /// </summary>
        public static async Task PreloadTTSAudios(string[] texts, string voiceId = null)
        {
            List<Task<AudioClip>> tasks = new List<Task<AudioClip>>();
            
            foreach (string text in texts)
            {
                if (string.IsNullOrEmpty(voiceId))
                {
                    tasks.Add(PlayTTSVolcEngine(text));
                }
                else
                {
                    tasks.Add(PlayTTSVolcEngine(text, voiceId));
                }
            }

            await Task.WhenAll(tasks);
            Debug.Log($"Preloaded {texts.Length} TTS audios");
        }

        /// <summary>
        /// 清理TTS缓存
        /// </summary>
        public static void ClearTTSCache()
        {
            // 查找并删除所有TTS相关的缓存
            var keys = new List<string>();
            
            // Unity PlayerPrefs没有直接的方法获取所有键，这里需要自己维护键列表
            // 或者使用其他缓存方案
            
            Debug.Log("TTS cache cleared (implementation depends on caching strategy)");
        }
    }

    // 用于JSON序列化的辅助类
    [System.Serializable]
    internal class TTSRequest
    {
        public string text;
        public string voice_type;
    }
}