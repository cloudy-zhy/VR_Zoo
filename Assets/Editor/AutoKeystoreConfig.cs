using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class AutoKeystoreConfig
{
    static AutoKeystoreConfig()
    {
        // 密钥库文件名（请根据实际文件名修改）
        string keystoreName = "user.keystore";

        // 组合完整路径
        string keystorePath = Path.Combine(Directory.GetCurrentDirectory(), keystoreName);

        if (File.Exists(keystorePath))
        {
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = "123456";
            PlayerSettings.Android.keyaliasName = "key0";
            PlayerSettings.Android.keyaliasPass = "123456";

            UnityEngine.Debug.Log($"已自动设置密钥库: {keystorePath}");
        }
        else
        {
            // 可以尝试在项目内搜索
            string[] foundFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.keystore", SearchOption.AllDirectories);
            if (foundFiles.Length > 0)
            {
                PlayerSettings.Android.keystoreName = foundFiles[0];
                PlayerSettings.Android.keystorePass = "123456";
                PlayerSettings.Android.keyaliasName = "key0";
                PlayerSettings.Android.keyaliasPass = "123456";

                UnityEngine.Debug.Log($"已自动设置密钥库: {foundFiles[0]}");
            }
        }
    }
}