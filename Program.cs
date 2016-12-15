using System;
using System.Configuration;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace EaseSvrSwitcher
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "易致接入服务器-切换中...";

            //-s \"{1}\" -base \"{2}\" +log  \"{3}\" \"%CD%\"

            DateTime taskStartTime = DateTime.Now;
            DateTime lastTime = DateTime.Now;
            string updateBaseDir = null, updateSvrName = null;
            string logFilePath = null, updateSrcDir = null;
            bool closeServiceFirst = true; //更新是是否先停止服务运行
            bool forectUIDFileExist = false; //执行操作必须更新文件存在
            bool restartServerOnly = false;  //仅重启服务

            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "+l")
                    {
                        logFilePath = args[++i];
                    }
                    else if (args[i] == "-n")
                    {
                        closeServiceFirst = false; //不必关闭服务更新
                    }
                    else if (args[i] == "-m")
                    {
                        forectUIDFileExist = true; //不存在更新会话文件则不再更新
                        restartServerOnly = true;
                    }
                    else if (args[i] == "-r")
                    {
                        restartServerOnly = true;
                    }
                    else if (args[i] == "-s")
                    {
                        updateSvrName = args[++i];
                    }
                    else if (args[i] == "-b")
                    {
                        updateBaseDir = args[++i];
                    }
                }
                updateSrcDir = args[args.Length - 1];
            }

            if (updateSvrName == null || (!restartServerOnly && (updateSrcDir == null || updateBaseDir == null)))
            {
                Console.WriteLine("参数错误：必须执行-s [服务名称] -b [更新目录] 以及文件来源目录参数");
            }
            else
            {
                if (logFilePath != null && !File.Exists(logFilePath))
                {
                    if (forectUIDFileExist)
                        return;
                }

                if (logFilePath != null && File.Exists(logFilePath))
                {
                    using (StreamWriter sw = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        sw.WriteLine(lastTime.ToString("<<< yyyy-MM-dd HH:mm:ss,fff") + " 服务切换程序接收到更新请求.<br/>");

                        sw.WriteLine("更新系统服务为:" + updateSvrName + "<br/>");
                        if (!restartServerOnly)
                        {
                            sw.WriteLine("目标更新目录" + updateBaseDir + "<br/>");
                            sw.WriteLine("文件来源目录" + updateSrcDir + "<br/>");
                        }
                        lastTime = DateTime.Now;
                        overrideDirTask(updateSvrName, updateSrcDir, updateBaseDir, sw, closeServiceFirst);

                        sw.WriteLine(">>> 全部更新完成，此次更新耗时{0}！<br/>", DateTime.Now - lastTime);
                    }
                }
                else
                {
                    overrideDirTask(updateSvrName, updateSrcDir, updateBaseDir, Console.Out, closeServiceFirst);
                }
            }

            using (Process currentProcess = Process.GetCurrentProcess())
            {
                currentProcess.Kill();
                currentProcess.Close();
                currentProcess.Dispose();
            }
            Console.WriteLine("程序退出！");
        }

        static void overrideDirTask(string svrName, string srcDir, string targetDir, TextWriter writer, bool restartSvr)
        {
            if (writer == null) return;

            WinSvrController wsc = new WinSvrController(svrName);
            if (!wsc.IsExist())
            {
                writer.WriteLine("服务(列表)" + svrName + "至少有一个不存在！<br/>");
            }
            else
            {
                if (restartSvr)
                {
                    writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff") + " 开始停止服务...<br/>");
                    wsc.StopService(5);
                }

                writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff") + " CLR GC回收资源...<br/>");
                GC.Collect();

                int totalCopyed = 0;
                #region 如果同步目录存在
                if (srcDir != null && Directory.Exists(srcDir)
                    && targetDir != null && Directory.Exists(targetDir))
                {

                    ManualResetEvent cpyEvt = new ManualResetEvent(false);
                    int retryTimes = 0;
                    Thread workThread = new Thread(new ThreadStart(() =>
                    {
                        merginDirWith(srcDir, targetDir, writer, ref totalCopyed, ref retryTimes);
                        cpyEvt.Set();
                    }));
                    workThread.Start();
                    try
                    {
                        int waitTotal = Convert.ToInt32(ConfigurationManager.AppSettings["EaseSvrSwitcher.MaxOverrideMs"] ?? "10000");
                        if (!workThread.Join(waitTotal)
                            && totalCopyed < 1)
                        {
                            writer.WriteLine(" #合并文件失败，" + waitTotal + "ms内没有完成复制任何文件(已重试" + retryTimes + "次)！<br/>");
                            workThread.Abort();
                            cpyEvt.Set();
                        }
                    }
                    catch (ThreadAbortException) { }
                    cpyEvt.WaitOne(-1);
                }
                #endregion

                if (restartSvr)
                {
                    writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff") + " 开始启动服务, 共更新" + totalCopyed + "个文件...<br/>");
                    wsc.StartService();
                    writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff") + " 服务启动完成.<br/>");
                }
                else
                {
                    writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff") + " 服务更新完成.<br/>");
                }
            }
        }

        static void merginDirWith(string srcDir, string tarDir, TextWriter writer, ref int execCount, ref int retryTimes)
        {
            bool hasError = false;

        WaitingForOverride:
            if (hasError)
                System.Threading.Thread.Sleep(500);

            DirectoryInfo crtDi = new DirectoryInfo(srcDir);
            int dirLen = crtDi.FullName.Length;
            if (crtDi.Exists)
            {
                foreach (FileInfo fi in crtDi.GetFiles("*.*", SearchOption.TopDirectoryOnly))
                {

                    if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;

                    #region 针对特定sql文件执行更新
                    if (fi.Name.EndsWith(".update.sql", StringComparison.InvariantCultureIgnoreCase))
                    {
                        writer.WriteLine(" #执行SQL脚本{0}。<br/>", fi.FullName.Substring(dirLen));
                        try
                        {
                            execSqlScript(fi.FullName, writer);
                        }
                        catch (Exception sqlEx)
                        {
                            writer.WriteLine(" #执行SQL脚本异常{0}。<br/>", sqlEx.Message);
                        }
                        continue;
                    }
                    #endregion

                    string tarFilePath = Path.Combine(tarDir, fi.Name);
                    if (File.Exists(tarFilePath))
                    {
                        File.SetAttributes(tarFilePath, FileAttributes.Normal);
                    }

                    try
                    {
                        File.Copy(fi.FullName, tarFilePath, true);
                        hasError = false;
                        execCount++;
                    }
                    catch (Exception cEx)
                    {
                        hasError = true;
                        if (writer != null)
                        {
                            bool fileLocked = (cEx is IOException && ((IOException)cEx).IsFileLocked());
                            writer.WriteLine(" #重试{1}, 复制文件失败{0}, 文件锁定{2}。<br/>", fi.FullName.Substring(dirLen), retryTimes, fileLocked);
                            if (!fileLocked)
                                writer.WriteLine(cEx + "<br/>");
                        }
                        break;
                    }
                }

                if (hasError && retryTimes < Convert.ToInt32(ConfigurationManager.AppSettings["EaseSvrSwitcher.MaxTryTimes"] ?? "20"))
                {
                    retryTimes++;
                    goto WaitingForOverride;
                }

                if (!hasError)
                {
                    DirectoryInfo[] subDirs = crtDi.GetDirectories("*.*", SearchOption.TopDirectoryOnly);
                    foreach (DirectoryInfo di in subDirs)
                    {
                        string tarDirPath = Path.Combine(tarDir, di.Name);
                        tarDirPath.ForceCreateDirectory();
                        merginDirWith(di.FullName, tarDirPath, writer, ref execCount, ref retryTimes);
                    }
                }
            }
        }

        static void execSqlScript(string sqlScriptPath, TextWriter writer)
        {
            ConnectionStringSettings connSetting = ConfigurationManager.ConnectionStrings["EaseSvrSwitcher.SqlConn"];
            string currentSqlExec = Encoding.UTF8.GetString(File.ReadAllBytes(sqlScriptPath));

            DbProviderFactory fac = DbProviderFactories.GetFactory(connSetting.ProviderName);
            using (DbConnection conn = fac.CreateConnection())
            {
                conn.ConnectionString = connSetting.ConnectionString;
                try
                {
                    conn.Open();
                    #region 执行SQL语句命令
                    string[] sqlSnippets = Regex.Split(currentSqlExec, "GO(\\r?\\n)?$");
                    foreach (string sql in sqlSnippets)
                    {
                        #region 兼容GO语句终止符号
                        using (DbCommand cmd = fac.CreateCommand())
                        {
                            cmd.Connection = conn;
                            cmd.CommandText = sql;
                            DbDataReader reader = cmd.ExecuteReader();
                        readData:
                            #region 读取数据集
                            int rowCount = 0;
                            StringBuilder colBuilder = new StringBuilder();
                            while (reader.Read())
                            {
                                if (colBuilder.Length < 1)
                                {
                                    for (int i = 0, j = reader.FieldCount; i < j; i++)
                                    {
                                        colBuilder.AppendFormat("{0}", (i == 0) ? " " : " , ");
                                        colBuilder.AppendFormat("[{0}]", reader.GetName(i));
                                    }
                                    writer.WriteLine(colBuilder.ToString() + "<br/>");
                                }

                                StringBuilder rowBuilder = new StringBuilder();
                                for (int i = 0, j = reader.FieldCount; i < j; i++)
                                {
                                    rowBuilder.AppendFormat("{0}", (i == 0) ? "[" + (++rowCount) + "] " : " , ");
                                    if (reader.GetFieldType(i).Equals(typeof(string)))
                                    {
                                        rowBuilder.AppendFormat("\"{0}\"", reader[i]);
                                    }
                                    else
                                    {
                                        rowBuilder.AppendFormat("{0}", reader[i]);
                                    }
                                }
                                writer.WriteLine(rowBuilder.ToString() + "<br/>");
                            }
                            #endregion
                            if (reader.NextResult()) goto readData;
                            reader.Close();
                        }
                        #endregion

                    }
                    #endregion
                    conn.Close();
                }
                catch (Exception sqlEx)
                {
                    writer.WriteLine(sqlEx.Message + "<br/>");
                }
            }
        }
    }
}
