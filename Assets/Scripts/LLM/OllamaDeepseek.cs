using UnityEngine;
using System.Threading.Tasks;
using System.Text;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace Immortal.LLM
{
    public interface IOllamaOptions
    {
        string model { get; set; }   // 模型名，默认 deepseek
        string url { get; set; }     // 服务地址，默认 http://localhost:11434/api/generate
        bool stream { get; set; }
        Dictionary<string, object> additionalOptions { get; set; }
    }

    [System.Serializable]
    public class OllamaOptions : IOllamaOptions
    {
        public string model { get; set; } = "deepseek-r1:14b";
        public string url { get; set; } = "http://localhost:11434/api/generate";
        public bool stream { get; set; } = false;
        public Dictionary<string, object> additionalOptions { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 本地Ollama服务 deepseek 模型文本生成模块
    /// </summary>
    public static class OllamaDeepseek
    {
        public static string DefaultUrl = "http://localhost:11434/api/generate";
        public static string DefaultModel = "deepseek-r1:14b";

        /// <summary>
        /// 调用本地Ollama服务生成文本
        /// </summary>
        /// <param name="prompt">输入提示</param>
        /// <param name="options">可选参数</param>
        /// <returns>Task&lt;string&gt;</returns>
        public static async Task<string> Generate(string prompt, OllamaOptions options = null)
        {
            if (options == null)
            {
                options = new OllamaOptions();
            }

            string url = !string.IsNullOrEmpty(options.url) ? options.url : DefaultUrl;
            string model = !string.IsNullOrEmpty(options.model) ? options.model : DefaultModel;

            // 构建请求数据
            var requestData = new Dictionary<string, object>
            {
                ["model"] = model,
                ["prompt"] = prompt,
                ["stream"] = false
            };

            // 添加额外选项
            if (options.additionalOptions != null)
            {
                foreach (var kvp in options.additionalOptions)
                {
                    if (!requestData.ContainsKey(kvp.Key))
                    {
                        requestData[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 确保关键参数正确
            requestData["prompt"] = prompt;
            requestData["model"] = model;

            string jsonBody = JsonUtility.ToJson(new SerializableDict(requestData));

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
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
                    throw new System.Exception($"OllamaDeepseek: HTTP {request.responseCode} - {request.error}");
                }

                string responseText = request.downloadHandler.text;
                
                try
                {
                    var response = JsonUtility.FromJson<OllamaResponse>(responseText);
                    return response.response ?? string.Empty;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to parse Ollama response: {e.Message}");
                    Debug.LogError($"Response text: {responseText}");
                    throw new System.Exception($"Failed to parse Ollama response: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 流式生成文本（简化实现，实际可能需要更复杂的流处理）
        /// </summary>
        public static async Task<string> GenerateStream(string prompt, System.Action<string> onPartialResponse, OllamaOptions options = null)
        {
            if (options == null)
            {
                options = new OllamaOptions();
            }

            options.stream = true;
            
            // 注意：Unity的UnityWebRequest不直接支持流式响应
            // 这里提供一个简化的实现，实际应用中可能需要使用其他HTTP客户端
            Debug.LogWarning("Stream mode not fully implemented with UnityWebRequest. Using regular generation.");
            
            return await Generate(prompt, options);
        }

        /// <summary>
        /// 检查Ollama服务是否可用
        /// </summary>
        public static async Task<bool> IsServiceAvailable(string url = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                url = DefaultUrl;
            }

            try
            {
                // 发送一个简单的测试请求
                var testOptions = new OllamaOptions
                {
                    url = url,
                    model = DefaultModel
                };

                await Generate("test", testOptions);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置默认配置
        /// </summary>
        public static void SetDefaults(string url, string model)
        {
            if (!string.IsNullOrEmpty(url))
            {
                DefaultUrl = url;
            }
            if (!string.IsNullOrEmpty(model))
            {
                DefaultModel = model;
            }
        }
    }

    // 用于序列化字典的辅助类
    [System.Serializable]
    internal class SerializableDict
    {
        public string model;
        public string prompt;
        public bool stream;

        public SerializableDict(Dictionary<string, object> dict)
        {
            if (dict.ContainsKey("model")) model = dict["model"].ToString();
            if (dict.ContainsKey("prompt")) prompt = dict["prompt"].ToString();
            if (dict.ContainsKey("stream")) stream = (bool)dict["stream"];
        }
    }

    // Ollama API 响应结构
    [System.Serializable]
    internal class OllamaResponse
    {
        public string model;
        public string created_at;
        public string response;
        public bool done;
        public int[] context;
        public long total_duration;
        public long load_duration;
        public int prompt_eval_count;
        public long prompt_eval_duration;
        public int eval_count;
        public long eval_duration;
    }
}