using System.IO;
using System.Text;

namespace JiYuKiller.Core
{
    /// <summary>
    /// "重要电脑"标记（原版 C:\Users\Public\promise.jy）。
    /// 文件存在时程序拒绝运行；在免责声明窗口点击"退出"时写入。
    /// </summary>
    internal static class PromiseMark
    {
        public const string MarkContent = "I don't want any trouble.";

        public static string MarkPath
        {
            get { return @"C:\Users\Public\promise.jy"; }
        }

        public static bool IsMarked()
        {
            if (!File.Exists(MarkPath))
                return false;
            try
            {
                string content = File.ReadAllText(MarkPath, Encoding.UTF8).Trim();
                return content == MarkContent;
            }
            catch
            {
                return false;
            }
        }

        public static void Write()
        {
            File.WriteAllText(MarkPath, MarkContent, new UTF8Encoding(false));
        }
    }
}
