using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class UITool : EditorWindow
{
    [MenuItem("Tools/Analyze UI Images")]
    public static void AnalyzeImages()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UI/Sprites" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (tex != null && importer != null)
            {
                Debug.Log($"Texture: {tex.name}, Size: {tex.width}x{tex.height}, Border: {importer.spriteBorder}");
            }
        }
    }
}
