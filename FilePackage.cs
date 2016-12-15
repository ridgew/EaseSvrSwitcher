using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Serialization;

namespace EaseSvrSwitcher
{
    /// <summary>
    /// 文件更新包
    /// </summary>
    [Serializable]
    public class FilePackage
    {
        /// <summary>
        /// 更新包类型
        /// </summary>
        [XmlAttribute]
        public PackageType Type { get; set; }

        /// <summary>
        /// 包更新根标识，可以为zip文件地址或系统文件目录
        /// </summary>
        [XmlAttribute]
        public string PackageRoot { get; set; }

        /// <summary>
        /// 更新包说明
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 所有包内文件
        /// </summary>
        public PackageItem[] Files { get; set; }

        /// <summary>
        /// 从zip文件或更新目录创建更新包
        /// </summary>
        /// <param name="zipFileOrDir">zip文件或更新目录</param>
        /// <param name="ignoreDirNames">忽略打包的目录名称</param>
        /// <returns></returns>
        public static FilePackage CreateFrom(string zipFileOrDir, string[] ignoreDirNames)
        {
            FilePackage pkg = new FilePackage();
            pkg.PackageRoot = zipFileOrDir;
            if (File.Exists(zipFileOrDir) && zipFileOrDir.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                pkg.Type = PackageType.Zip;
            }
            else
            {
                if (Directory.Exists(zipFileOrDir))
                    pkg.Type = PackageType.Directory;
            }
            if (pkg.Type == PackageType.UnKnown)
            {
                throw new InvalidDataException("指定更新包参数错误！");
            }
            else
            {
                List<PackageItem> fileList = new List<PackageItem>();
                if (pkg.Type == PackageType.Zip)
                {
                    #region 处理zip包
                    using (ZipStorer zip = ZipStorer.Open(zipFileOrDir, FileAccess.Read))
                    {
                        pkg.Comment = zip.Comment;
                        zip.ReadCentralDirAction(zfe =>
                        {
                            fileList.Add(new PackageItem { FilePath = zfe.FilenameInZip, FileSize = zfe.FileSize, LastModified = ZipStorer.DateTimeToDosTime(zfe.ModifyTime) });
                        });
                    }
                    #endregion
                }

                if (pkg.Type == PackageType.Directory)
                {
                    DirectoryInfo cFS = new DirectoryInfo(zipFileOrDir);
                    int trimLen = cFS.FullName.Length;
                    doDirectorySystemAction(cFS, ignoreDirNames, f =>
                    {
                        fileList.Add(new PackageItem
                        {
                            FilePath = f.FullName.Substring(trimLen).TrimStart('/', '\\'),
                            FileSize = f.Length,
                            LastModified = ZipStorer.DateTimeToDosTime(f.LastWriteTime)
                        });

                    });
                }
                pkg.Files = fileList.ToArray();
            }
            return pkg;
        }

        /// <summary>
        /// 处理文件基于文件目录的操作
        /// </summary>
        /// <param name="dInfo">文件目录对象</param>
        /// <param name="ignoreDirNames">The ignore dir names.</param>
        /// <param name="fInfoAction">The f info action.</param>
        internal static void doDirectorySystemAction(DirectoryInfo dInfo, string[] ignoreDirNames, Action<FileInfo> fInfoAction)
        {
            foreach (FileInfo f in dInfo.GetFiles())
            {
                fInfoAction(f);
            }
            foreach (DirectoryInfo d in dInfo.GetDirectories())
            {
                if (Array.IndexOf<string>(ignoreDirNames, d.Name) != -1)
                {
                    doDirectorySystemAction(d, ignoreDirNames, fInfoAction);
                }
            }
        }


    }

    /// <summary>
    /// 包类型
    /// </summary>
    public enum PackageType
    {
        /// <summary>
        /// 未知
        /// </summary>
        UnKnown = 0,
        /// <summary>
        /// 系统目录
        /// </summary>
        Directory = 1,
        /// <summary>
        /// zip文件
        /// </summary>
        Zip = 2
    }

    /// <summary>
    /// 更新包内文件
    /// </summary>
    [Serializable]
    public struct PackageItem
    {
        /// <summary>
        /// 包内文件路径
        /// </summary>
        [XmlAttribute]
        public string FilePath { get; set; }


        /// <summary>
        /// 是否需要删除项
        /// </summary>
        [XmlAttribute]
        public bool IsDelete { get; set; }


        /// <summary>
        /// 文件字节数
        /// </summary>
        [XmlAttribute]
        public long FileSize { get; set; }

        /// <summary>
        /// 最新修改时间(DOS日期)
        /// </summary>
        [XmlAttribute]
        public uint LastModified { get; set; }
    }

}
