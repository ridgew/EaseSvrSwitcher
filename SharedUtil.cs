using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace EaseSvrSwitcher
{
    /// <summary>
    /// 共享工具类
    /// </summary>
    public static class SharedUtil
    {
        /// <summary>
        /// 在限制秒数内的执行相关操作，并返回是否超时(默认20秒)。
        /// </summary>
        /// <param name="act">相关方法操作</param>
        /// <param name="timeoutSeconds">超时秒数</param>
        /// <returns>操作是否超时</returns>
        public static bool ExecTimeoutMethod(this Action act, int? timeoutSeconds)
        {
            bool isTimeout = false;
            Thread workThread = new Thread(new ThreadStart(act));
            workThread.Start();
            try
            {
                if (!workThread.Join((timeoutSeconds.HasValue && timeoutSeconds.Value > 0) ? timeoutSeconds.Value * 1000 : 20000))
                {
                    workThread.Abort();
                    isTimeout = true;
                }
            }
            catch (ThreadAbortException) { }
            return isTimeout;
        }

        /// <summary>
        /// 关闭委托
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(this IntPtr hObject);

        /// <summary>
        /// 判断异常是否产生于文件锁定
        /// <para>http://stackoverflow.com/questions/1304/how-to-check-for-file-lock-in-c</para>
        /// </summary>
        public static bool IsFileLocked(this IOException exception)
        {
            int errorCode = Marshal.GetHRForException(exception) & ((1 << 16) - 1);
            return errorCode == 32 || errorCode == 33;
        }

        //public static void test()
        //{
        //    IntPtr p = new IntPtr(0x02d0);
        //    p.CloseHandle();
        //}


        /// <summary>
        /// 强制退出文件的进程
        /// </summary>
        /// <param name="imageFilename">印象文件名称</param>
        public static void KillProcessImage(this string imageFilename)
        {
            Process[] tarProcs = Process.GetProcessesByName(imageFilename);
            if (tarProcs != null && tarProcs.Length > 0)
            {
                foreach (Process sp in tarProcs)
                {
                    try
                    {
                        sp.Kill();
                    }
                    catch (Exception) { }

                    try
                    {
                        sp.Close();
                    }
                    catch (Exception) { }

                    try
                    {
                        sp.Dispose();
                    }
                    catch (Exception) { }
                }
            }
        }

        public static bool ProcessImageRunning(this string imageFilename)
        {
            Process[] tarProcs = Process.GetProcessesByName(imageFilename);
            return (tarProcs != null && tarProcs.Length > 0);
        }

        /// <summary>
        /// 判断是否存在文件路径的目录，如不存在则创建
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public static bool ForceCreateDirectory(this string filePath)
        {
            string dirName = Path.GetDirectoryName(filePath);
            if (Directory.Exists(dirName))
            {
                return true;
            }
            else
            {
                bool created = false;
                try
                {
                    Directory.CreateDirectory(dirName);
                    created = true;
                }
                catch (Exception) { }
                return created;
            }
        }

        /// <summary>
        /// 强制删除指定目录的文件，如果删除成功则返回true。
        /// </summary>
        /// <param name="filePath">目录文件地址</param>
        /// <returns></returns>
        public static bool ForceDeleteFile(this string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            bool fDeleted = false;
            try
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
                fDeleted = true;
            }
            catch (Exception) { }
            return fDeleted;
        }

    }
}
