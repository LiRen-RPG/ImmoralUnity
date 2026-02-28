using UnityEngine;
using Immortal.Core;

namespace Immortal.LLM
{
    /// <summary>
    /// 年龄组枚举
    /// </summary>
    public enum AgeGroup
    {
        CHILD = 0,       // 儿童 (0-7岁)
        TEENAGER = 1,    // 青少年 (8-15岁) 
        YOUNG_ADULT = 2, // 青年 (16-30岁)
        MIDDLE_AGED = 3, // 中年 (31-60岁)
        ELDERLY = 4      // 老年 (61+岁)
    }

    /// <summary>
    /// 字节跳动TTS语音配置管理
    /// 根据性别、年龄和性格特征智能选择合适的语音
    /// </summary>
    public static class BytedanceVoices
    {
        // 女性语音矩阵 - 按年龄组和性格排列
        private static readonly string[][] femaleVoiceMatrix = new string[][]
        {
            // 儿童组 (CHILD) - 从内向到外向
            new string[]
            {
                "zh_female_xuanxuan_moon_bigtts",
                "zh_female_beijingQ_conversation_wvae_bigtts"
            },
            
            // 青少年组 (TEENAGER) - 从内向到外向
            new string[]
            {
                "zh_female_qingxin_conversation_wvae_bigtts",
                "zh_female_yuyu_conversation_wvae_bigtts",
                "zh_female_wanwangwan_conversation_wvae_bigtts"
            },
            
            // 青年组 (YOUNG_ADULT) - 从内向到外向
            new string[]
            {
                "zh_female_wanwangwan_conversation_wvae_bigtts",
                "zh_female_qingyan_conversation_wvae_bigtts",
                "zh_female_xuehao_conversation_wvae_bigtts",
                "zh_female_shuangkuaicuihua_conversation_wvae_bigtts"
            },
            
            // 中年组 (MIDDLE_AGED) - 从内向到外向
            new string[]
            {
                "zh_female_jiajia_conversation_wvae_bigtts",
                "zh_female_xuehao_conversation_wvae_bigtts"
            },
            
            // 老年组 (ELDERLY) - 从内向到外向
            new string[]
            {
                "zh_female_beijing_xierxue_conversation_wvae_bigtts"
            }
        };

        // 男性语音矩阵 - 按年龄组和性格排列
        private static readonly string[][] maleVoiceMatrix = new string[][]
        {
            // 儿童组 (CHILD) - 从内向到外向
            new string[]
            {
                "zh_male_xiaoxiang_moon_bigtts",
                "zh_male_xiaoqiang_moon_bigtts"
            },
            
            // 青少年组 (TEENAGER) - 从内向到外向
            new string[]
            {
                "zh_male_qingliangsaizhu_conversation_wvae_bigtts",
                "zh_male_xiaoyu_conversation_wvae_bigtts",
                "zh_male_beijing_xierxue_conversation_wvae_bigtts"
            },
            
            // 青年组 (YOUNG_ADULT) - 从内向到外向
            new string[]
            {
                "zh_male_M392_conversation_wvae_bigtts",
                "zh_male_xiaoyu_conversation_wvae_bigtts",
                "zh_male_shuangkuaicuihua_conversation_wvae_bigtts",
                "zh_male_qingliangsaizhu_conversation_wvae_bigtts"
            },
            
            // 中年组 (MIDDLE_AGED) - 从内向到外向
            new string[]
            {
                "zh_male_dongfanghaoran_moon_bigtts",
                "zh_male_silang_mars_bigtts",
                "zh_male_qingcang_mars_bigtts"
            },
            
            // 老年组 (ELDERLY) - 从内向到外向
            new string[]
            {
                "ICL_zh_male_youmodaye_tob"
            }
        };

        /// <summary>
        /// 根据性别、年龄和性格浮点值获取对应的语音ID
        /// </summary>
        /// <param name="gender">性别</param>
        /// <param name="age">年龄 (0-100)</param>
        /// <param name="personality">性格值 (0.0 = 完全内向, 1.0 = 完全外向)</param>
        /// <returns>对应的字节跳动TTS语音ID</returns>
        public static string GetVoiceByGenderAgeAndPersonality(Gender gender, int age, float personality)
        {
            // 将年龄转换为年龄组
            AgeGroup ageGroup;
            if (age <= 7)
            {
                ageGroup = AgeGroup.CHILD;
            }
            else if (age <= 15)
            {
                ageGroup = AgeGroup.TEENAGER;
            }
            else if (age <= 30)
            {
                ageGroup = AgeGroup.YOUNG_ADULT;
            }
            else if (age <= 60)
            {
                ageGroup = AgeGroup.MIDDLE_AGED;
            }
            else
            {
                ageGroup = AgeGroup.ELDERLY;
            }

            string[][] voiceMatrix = gender == Gender.Male ? maleVoiceMatrix : femaleVoiceMatrix;

            if ((int)ageGroup < 0 || (int)ageGroup >= voiceMatrix.Length)
            {
                throw new System.ArgumentException($"Invalid age group: {ageGroup}");
            }

            // 确保personality值在0-1范围内
            personality = Mathf.Max(0f, Mathf.Min(1f, personality));

            string[] voicesForAge = voiceMatrix[(int)ageGroup];
            if (voicesForAge.Length == 0)
            {
                throw new System.ArgumentException($"No voices available for age group: {ageGroup}");
            }

            // 如果只有一个语音，直接返回
            if (voicesForAge.Length == 1)
            {
                return voicesForAge[0];
            }

            // 根据personality值计算索引
            // personality从0到1映射到0到(length-1)
            int index = Mathf.FloorToInt(personality * (voicesForAge.Length - 1) + 0.5f);
            int clampedIndex = Mathf.Max(0, Mathf.Min(voicesForAge.Length - 1, index));

            return voicesForAge[clampedIndex];
        }

        /// <summary>
        /// 获取男性语音ID
        /// </summary>
        /// <param name="age">年龄 (0-100)</param>
        /// <param name="personality">性格值 (0.0 = 弱势, 1.0 = 强势)</param>
        /// <returns>对应的男性语音ID</returns>
        public static string GetMaleVoice(int age, float personality)
        {
            return GetVoiceByGenderAgeAndPersonality(Gender.Male, age, personality);
        }

        /// <summary>
        /// 获取女性语音ID
        /// </summary>
        /// <param name="age">年龄 (0-100)</param>
        /// <param name="personality">性格值 (0.0 = 弱势, 1.0 = 强势)</param>
        /// <returns>对应的女性语音ID</returns>
        public static string GetFemaleVoice(int age, float personality)
        {
            return GetVoiceByGenderAgeAndPersonality(Gender.Female, age, personality);
        }

        /// <summary>
        /// 获取所有可用的语音配置
        /// </summary>
        /// <returns>包含性别、年龄组和可用语音数量的配置数组</returns>
        public static VoiceConfig[] GetAllVoiceConfigs()
        {
            System.Collections.Generic.List<VoiceConfig> configs = new System.Collections.Generic.List<VoiceConfig>();

            // 添加男性语音配置
            for (int age = 0; age < maleVoiceMatrix.Length; age++)
            {
                configs.Add(new VoiceConfig
                {
                    gender = Gender.Male.ToString(),
                    ageGroup = ((AgeGroup)age).ToString(),
                    voiceCount = maleVoiceMatrix[age].Length,
                    voices = maleVoiceMatrix[age]
                });
            }

            // 添加女性语音配置
            for (int age = 0; age < femaleVoiceMatrix.Length; age++)
            {
                configs.Add(new VoiceConfig
                {
                    gender = Gender.Female.ToString(),
                    ageGroup = ((AgeGroup)age).ToString(),
                    voiceCount = femaleVoiceMatrix[age].Length,
                    voices = femaleVoiceMatrix[age]
                });
            }

            return configs.ToArray();
        }

        /// <summary>
        /// 根据Cultivator获取合适的语音ID
        /// </summary>
        /// <param name="cultivator">修仙者对象</param>
        /// <returns>合适的语音ID</returns>
        public static string GetVoiceForCultivator(Cultivator cultivator)
        {
            return GetVoiceByGenderAgeAndPersonality(
                cultivator.gender, 
                (int)cultivator.appearanceAge, 
                cultivator.personality
            );
        }

        /// <summary>
        /// 获取随机语音ID
        /// </summary>
        /// <param name="gender">性别</param>
        /// <param name="ageGroup">年龄组</param>
        /// <returns>随机选择的语音ID</returns>
        public static string GetRandomVoice(Gender gender, AgeGroup ageGroup)
        {
            string[][] voiceMatrix = gender == Gender.Male ? maleVoiceMatrix : femaleVoiceMatrix;

            if ((int)ageGroup < 0 || (int)ageGroup >= voiceMatrix.Length)
            {
                throw new System.ArgumentException($"Invalid age group: {ageGroup}");
            }

            string[] voicesForAge = voiceMatrix[(int)ageGroup];
            if (voicesForAge.Length == 0)
            {
                throw new System.ArgumentException($"No voices available for age group: {ageGroup}");
            }

            int randomIndex = Random.Range(0, voicesForAge.Length);
            return voicesForAge[randomIndex];
        }

        /// <summary>
        /// 检查语音ID是否有效
        /// </summary>
        /// <param name="voiceId">语音ID</param>
        /// <returns>如果语音ID存在则返回true</returns>
        public static bool IsValidVoiceId(string voiceId)
        {
            // 检查男性语音
            foreach (var ageGroup in maleVoiceMatrix)
            {
                foreach (var voice in ageGroup)
                {
                    if (voice == voiceId) return true;
                }
            }

            // 检查女性语音
            foreach (var ageGroup in femaleVoiceMatrix)
            {
                foreach (var voice in ageGroup)
                {
                    if (voice == voiceId) return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 语音配置信息
    /// </summary>
    [System.Serializable]
    public class VoiceConfig
    {
        public string gender;
        public string ageGroup;
        public int voiceCount;
        public string[] voices;
    }
}