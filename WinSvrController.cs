using System;
using System.IO;
using System.Management;
using System.ServiceProcess;

namespace EaseSvrSwitcher
{
    /// <summary>
    /// 服务控制器
    /// </summary>
    public class WinSvrController
    {

        /// <summary>
        /// 初始化一个 <see cref="WinSvrController"/> class 实例。
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        public WinSvrController(string serviceName)
        {
            _allServices = serviceName;
        }

        string _allServices = null;
        string[] listSvrs = null;
        TextWriter _log = null;

        string getSvrDirPath(string svrName)
        {
            string sql = "SELECT PathName from Win32_Service where Name =\"" + svrName + "\"";
            using (ManagementObjectSearcher Searcher = new ManagementObjectSearcher(sql))
            {
                foreach (ManagementObject service in Searcher.Get())
                {
                    return service["PathName"].ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// 判断改名称的服务是否存在
        /// </summary>
        /// <returns></returns>
        public bool IsExist()
        {
            string[] allsvrs = getListServices();
            foreach (string svr in allsvrs)
            {
                if (getSvrDirPath(svr) == null)
                    return false;
            }
            return true;
        }

        string[] getListServices()
        {
            if (listSvrs == null)
                listSvrs = _allServices.Split(new char[] { ',', '|', '+', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return listSvrs;
        }


        /// <summary>
        /// 在指定时间限制内关闭服务
        /// </summary>
        /// <param name="timeoutSeconds">超时秒数</param>
        public void StopService(int timeoutSeconds)
        {
            string[] allsvrs = getListServices();
            foreach (string svr in allsvrs)
            {
                LogWriterLine("停止服务 -> " + svr);
                stopSlnService(svr, timeoutSeconds);
            }
        }

        void stopSlnService(string svrName, int timeoutSeconds)
        {
            int tryTimes = 3, currentTimes = 0;
            string svrImgName = null;

        startStop:
            using (ServiceController sc = new ServiceController(svrName))
            {
                if (sc == null) return;
                svrImgName = Path.GetFileName(getSvrDirPath(svrName));

                if (sc.Status == ServiceControllerStatus.Running)
                {
                    bool timeout = false;
                    try
                    {
                        timeout = new Action(() =>
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped);
                        }).ExecTimeoutMethod(timeoutSeconds);
                    }
                    catch (System.Threading.ThreadAbortException) { }

                    if (timeout)
                    {
                        LogWriterLine("*{0}秒内关闭服务{2}超时,镜像名称{1}！", timeoutSeconds, svrImgName, svrName);
                        svrImgName.KillProcessImage();
                    }
                    sc.Refresh();
                    currentTimes++;
                }
            }

            if (svrImgName != null && svrImgName.ProcessImageRunning() && currentTimes <= tryTimes)
            {
                LogWriterLine("* 第{0}/{2}次重试停止服务{1}！", currentTimes, svrName, tryTimes);
                System.Threading.Thread.Sleep(50);
                goto startStop;
            }
        }

        void LogWriterLine(string fmt, params object[] args)
        {
            if (_log != null)
                _log.WriteLine(fmt, args);
        }

        void startSlnService(string svrName)
        {
            using (ServiceController sc = new ServiceController(svrName))
            {
                if (sc == null) return;

            stopSvrService:
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    stopSlnService(svrName, 5);
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(2.00));

                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Running)
                        goto stopSvrService;
                }

                try
                {
                    sc.Start();
                }
                catch (Exception)
                {
                    System.Threading.Thread.Sleep(50);
                    goto stopSvrService;
                }

                sc.WaitForStatus(ServiceControllerStatus.Running);
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void StartService()
        {
            string[] allsvrs = getListServices();
            for (int i = allsvrs.Length; i > 0; i--)
            {
                startSlnService(allsvrs[i - 1]);
                LogWriterLine("启动服务 -> " + allsvrs[i - 1]);
            }
        }

    }
}
