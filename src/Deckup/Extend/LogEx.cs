using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Deckup.Extend
{
    public static class LogEx
    {
        public static StringBuilder GetExceptionInfo(this Exception ex, string from = null, Assembly lib = null)
        {
            lib = lib ?? Assembly.GetEntryAssembly();

            StringBuilder builder = new StringBuilder();
            TextWriter writer = new StringWriter(builder);
            writer.WriteLine("/*");
            writer.WriteLine(" *系统版本：{0}", Environment.OSVersion.VersionString);
            writer.WriteLine(" *运行版本：{0}", (lib != null) ? lib.GetName().Version.ToString() : string.Empty);  //(Assembly.GetEntryAssembly() ?? Application.Current.GetType().Assembly).GetName().Version);
            writer.WriteLine(" *错误来源：{0}", from);
            writer.WriteLine(" *发生时间：{0}", DateTime.Now);
            writer.WriteLine(" *错误对象：{0}", ex.Source ?? string.Empty);
            writer.WriteLine(" *异常信息：{0}", ex.Message ?? string.Empty);
            writer.WriteLine(" *异常方法：{0}", (ex.TargetSite != null) ? ex.TargetSite.ToString() : string.Empty);
            writer.WriteLine(" *堆栈信息：{0}", (ex.StackTrace != null) ? ex.StackTrace.Replace("\n", "\n *") : string.Empty);
            if (ex.InnerException != null)
            {
                writer.WriteLine(" *原始错误对象：{0}", ex.InnerException.Source ?? string.Empty);
                writer.WriteLine(" *原始异常信息：{0}", ex.InnerException.Message ?? string.Empty);
                writer.WriteLine(" *原始异常方法：{0}", (ex.InnerException.TargetSite != null) ? ex.InnerException.TargetSite.ToString() : string.Empty);
                writer.WriteLine(" *原始堆栈信息：{0}", (ex.InnerException.StackTrace != null) ? ex.InnerException.StackTrace.Replace("\n", "\n *") : string.Empty);
            }
            writer.WriteLine(" */\n");
            return builder;
        }
    }
}