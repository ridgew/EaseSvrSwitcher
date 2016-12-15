using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonLib;
using System.Xml.Serialization;
using System.IO;
using System.IO.Compression;

namespace EaseSvrSwitcher
{
    /// <summary>
    /// 系统更新任务
    /// </summary>
    [Serializable]
    public class UpdateTask
    {
        /// <summary>
        /// 初始化一个 <see cref="UpdateTask"/> class 实例。
        /// </summary>
        /// <param name="currentTaskId">The current task id.</param>
        public UpdateTask(string currentTaskId)
        {
            _taskId = currentTaskId;
        }

        string _taskId = null;
        /// <summary>
        /// 获取当前更新任务的标识
        /// </summary>
        public string TaskID
        {
            get { return _taskId; }
        }

        #region 任务序列化
        /// <summary>
        /// 应用更新包的系统目录
        /// </summary>
        /// <value>系统目录</value>
        [XmlAttribute]
        public string TargetDirectory { get; set; }

        /// <summary>
        /// 备份类型
        /// </summary>
        [XmlAttribute]
        public BackUpCategory Category { get; set; }

        /// <summary>
        /// 更新产生的日志文件
        /// </summary>
        [XmlAttribute]
        public string LogFilePath { get; set; }

        #endregion

        FilePackage srcPkg = null;

        #region 流畅接口函数
        /// <summary>
        /// 设置备份时忽略的目录名称
        /// </summary>
        /// <param name="dirs">目录名称列表</param>
        /// <returns></returns>
        public UpdateTask WithIgnoreDirPattern(params string[] dirs)
        {
            return this;
        }

        /// <summary>
        /// 应用更新包
        /// </summary>
        /// <param name="pkg">更新包</param>
        /// <param name="targetDir">该更新包对应的更新目录</param>
        /// <returns></returns>
        public UpdateTask ApplyUpdate(FilePackage pkg, string targetDir)
        {
            srcPkg = pkg;
            TargetDirectory = targetDir;
            return this;
        }

        /// <summary>
        /// 设置覆盖时的操作
        /// </summary>
        /// <param name="act"></param>
        /// <returns></returns>
        public UpdateTask OnOverrideAction(Action act)
        {
            return this;
        }

        /// <summary>
        /// 设置备份类别
        /// </summary>
        /// <param name="category">备份类型</param>
        /// <returns></returns>
        public UpdateTask SetBackupCategory(BackUpCategory category)
        {
            Category = category;
            return this;
        }

        /// <summary>
        /// 保持所有操作进度到日志文件
        /// </summary>
        /// <param name="logFilePath">日志文件路径</param>
        /// <returns></returns>
        public UpdateTask ReportProgressTo(string logFilePath)
        {
            LogFilePath = logFilePath;
            return this;
        }
        #endregion

        /// <summary>
        /// 执行更新任务
        /// </summary>
        public void Execute()
        {
            /*
             先备份、后更新、改名
             a. 应用更新包、部分备份
             b. 完整备份、应用更新包
             */
            string bakDir = Path.Combine(TargetDirectory, "..\bak");

            string bakSrcFilePath = null;
            string bakFileDir = null, bakFilePath = null;
            bool isZipBak = Category.Has<BackUpCategory>(BackUpCategory.Zip);


            #region 备份

            if (Category.Has<BackUpCategory>(BackUpCategory.Full))
            {
                #region 全部备份
                #endregion
            }
            else
            {
                foreach (PackageItem item in srcPkg.Files)
                {
                    bakSrcFilePath = Path.Combine(TargetDirectory, item.FilePath);

                    if (File.Exists(bakSrcFilePath))
                    {
                        bakFilePath = Path.Combine(bakDir, item.FilePath);
                        bakFileDir = Path.GetDirectoryName(bakFilePath);
                        if (!Directory.Exists(bakFileDir))
                            Directory.CreateDirectory(bakFileDir);

                        File.Copy(bakSrcFilePath, bakFilePath, true);
                    }
                }
            }
            #endregion


            //指定覆盖前准备操作

            //[TODO]统一转换为文件目录方式操作

            #region 更新覆盖
            if (srcPkg.Type == PackageType.Directory)
            {
                foreach (PackageItem item in srcPkg.Files)
                {
                    bakSrcFilePath = Path.Combine(TargetDirectory, item.FilePath);
                    if (File.Exists(bakSrcFilePath))
                    {
                        File.SetAttributes(bakSrcFilePath, FileAttributes.Normal);
                        //旧文件存在
                        if (item.IsDelete)
                        {
                            File.Delete(bakSrcFilePath);
                        }
                        else
                        {
                            File.Copy(Path.Combine(srcPkg.PackageRoot, item.FilePath), bakSrcFilePath, true);
                        }
                    }
                }
            }
            #endregion
        }

        /// <summary>
        /// 使用更新包合并目标目录结构文件
        /// </summary>
        /// <param name="pkg">当前包</param>
        /// <param name="bkCfg">备份配置</param>
        /// <param name="targetDir">目标目录</param>
        static void meginDirWithFilePackage(FilePackage pkg, BackupConfig bkCfg, string targetDir)
        {
            bool isFullBacked = bkCfg.Category.Has<BackUpCategory>(BackUpCategory.Full);
            bool isZipBackup = bkCfg.Category.Has<BackUpCategory>(BackUpCategory.Zip);

            List<string> zipFileList = new List<string>();
            if (isFullBacked)
            {
                #region 全部备份，并压缩
                //TODO
                #endregion
            }


            if (pkg.Type == PackageType.Zip)
            {
                using (ZipStorer zip = ZipStorer.Open(pkg.PackageRoot, FileAccess.Read))
                {
                    zip.ReadCentralDirAction(zfe =>
                    {
                        string srcBkFilePath = Path.Combine(targetDir, zfe.FilenameInZip);
                        if (File.Exists(srcBkFilePath))
                        {
                            string destBkFilePath = Path.Combine(bkCfg.BackDirectory, zfe.FilenameInZip);
                            string deskBkDir = Path.GetDirectoryName(destBkFilePath);
                            if (!Directory.Exists(deskBkDir))
                                Directory.CreateDirectory(deskBkDir);

                            File.Copy(srcBkFilePath, destBkFilePath, true); //备份
                            if (bkCfg.Category.Has<BackUpCategory>(BackUpCategory.Zip))
                            {
                                zipFileList.Add(destBkFilePath); //添加到备份创建zip列表
                            }
                        }
                        zip.ExtractFile(zfe, srcBkFilePath);    //更新
                    });
                }
            }
            else
            {
                foreach (PackageItem item in pkg.Files)
                {
                    string bakSrcFilePath = Path.Combine(targetDir, item.FilePath);
                    #region 存在对应文件则备份
                    if (File.Exists(bakSrcFilePath))
                    {
                        string destBkFilePath = Path.Combine(bkCfg.BackDirectory, item.FilePath);
                        string deskBkDir = Path.GetDirectoryName(destBkFilePath);
                        if (!Directory.Exists(deskBkDir))
                            Directory.CreateDirectory(deskBkDir);

                        if (bkCfg.Category.Has<BackUpCategory>(BackUpCategory.Zip))
                        {
                            zipFileList.Add(destBkFilePath); //添加到备份创建zip列表
                        }

                        File.SetAttributes(bakSrcFilePath, FileAttributes.Normal);
                        if (item.IsDelete)
                        {
                            File.Delete(bakSrcFilePath); //更新为删除
                        }
                    }

                    if (bakSrcFilePath.ForceCreateDirectory())
                    {
                        File.Copy(Path.Combine(pkg.PackageRoot, item.FilePath), bakSrcFilePath, true);  //更新对应文件
                    }
                    #endregion
                }
            }

            #region 部分备份
            if (!isFullBacked && isZipBackup)
            {
                string zipFilePath = Path.Combine(bkCfg.BackDirectory, string.Concat("bak-", DateTime.Now.ToString("yyyyMMhhmmss"), ".zip"));
                using (ZipStorer zip = ZipStorer.Create(zipFilePath, bkCfg.BackComment))
                {
                    int bakDirLen = bkCfg.BackDirectory.Length;
                    foreach (string file in zipFileList)
                    {
                        zip.AddFile(ZipStorer.Compression.Deflate, file,
                            file.Substring(bakDirLen), "");

                        //添加后删除文件
                        file.ForceDeleteFile();
                    }
                }
            }
            #endregion

        }

    }

    /// <summary>
    /// 备份类别
    /// </summary>
    [Flags]
    public enum BackUpCategory
    {
        /// <summary>
        /// 部分备份
        /// </summary>
        Partial = 1,
        /// <summary>
        /// 完整备份
        /// </summary>
        Full = 2,
        /// <summary>
        /// 打包备份
        /// </summary>
        Zip = 4
    }


    /// <summary>
    /// 备份配置
    /// </summary>
    public class BackupConfig
    {
        /// <summary>
        /// 备份目录
        /// </summary>
        public string BackDirectory { get; set; }

        /// <summary>
        /// 备份类型
        /// </summary>
        public BackUpCategory Category { get; set; }

        /// <summary>
        /// 备份描述
        /// </summary>
        public string BackComment { get; set; }
    }

}
