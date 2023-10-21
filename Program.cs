using System;
using System.Collections.Generic;
using System.Linq;
using DSInternals.Common;
using DSInternals.Common.Data;
using DSInternals.DataStore;
using Alphaleonis.Win32.Vss;
using Alphaleonis.Win32.Filesystem;
using System.Security.Principal;

namespace getADHashes
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            if (!isElevated)
            {
                Console.WriteLine("Please run this app with administrator privileges.");
                Console.ReadKey();
                Environment.Exit(0);
            }
            //!
            byte[] key = BootKeyRetriever.GetBootKey();
            string keystr = key.ToHex();
            string destpath = "c:\\temp\\dstmp";
            string ntdsFilePath = "c:\\Windows\\NTDS";
            string basePath = AppDomain.CurrentDomain.BaseDirectory+"result";
            DirectoryInfo dest = new DirectoryInfo(destpath);

            Console.WriteLine("Copying NTDS.dit...");
            if (System.IO.Directory.Exists(destpath))
            {
                try
                {
                    RecursiveDelete(dest);
                }
                catch
                {
                }
                
            }
            if (System.IO.Directory.Exists(destpath))
            {
                try
                {
                    System.IO.Directory.Delete(destpath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
            System.IO.Directory.CreateDirectory(destpath);
            copyNTDS(ntdsFilePath, destpath);
            removeSnapshots();

            Console.WriteLine("Checking NTDS.dit...");
            checkNTDS(destpath);

            Console.WriteLine("Working with NTDS.dit...");
            List<string> accounts = getHashes(destpath+"\\NTDS.dit", keystr);

            writeResult(basePath, accounts);
            
            Console.WriteLine("Removing temporary files...");
            try
            {
                RecursiveDelete(dest);
            }
            catch
            {
            }
            if (System.IO.Directory.Exists(destpath))
            {
                try
                {
                    System.IO.Directory.Delete(destpath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
            Console.WriteLine("Operation completed. Results saved to the file - " + basePath + "\\users.csv");
            Console.ReadKey();

        }
        public static void writeResult(string basePath, List<string> accounts)
        {
            DirectoryInfo bPath = new DirectoryInfo(basePath);
            if (System.IO.Directory.Exists(basePath))
            {
                RecursiveDelete(bPath);
            }
            System.IO.Directory.CreateDirectory(basePath);
            System.IO.File.AppendAllLines(basePath + "\\users.csv", accounts);
        }
        public static void checkNTDS(string destpath)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.WorkingDirectory = destpath;
            startInfo.Arguments = "/C esentutl /r edb /d " + destpath + " /s " + destpath + " /l " + destpath;
            startInfo.Verb = "runas";
            process.StartInfo = startInfo;
            process.Start();
            process.Close();
        }
        public static void RecursiveDelete(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists)
                return;

            foreach (var dir in baseDir.EnumerateDirectories())
            {
                RecursiveDelete(dir);
            }
            var files = baseDir.GetFiles();
            foreach (var file in files)
            {
                file.IsReadOnly = false;
                file.Delete();
            }
            baseDir.Delete();
        }

        public static void removeSnapshots()
        {
            IVssFactory vssImplementation = VssFactoryProvider.Default.GetVssFactory();
            using (IVssBackupComponents backup = vssImplementation.CreateVssBackupComponents())
            {
                backup.InitializeForBackup(null);

                backup.SetContext(VssSnapshotContext.All);
                IEnumerable<VssSnapshotProperties> test = backup.QuerySnapshots();
                if (backup.QuerySnapshots().Count() > 0)
                {
                    foreach (VssSnapshotProperties prop in test)
                    {
                        backup.DeleteSnapshot(prop.SnapshotId, true);
                    }
                }
            }
        }
        public static void copyNTDS(string ntdsFilePath, string destpath)
        {
            if (System.IO.Directory.Exists(ntdsFilePath))
            {
                using (VssBackup vss = new VssBackup())
                {
                    vss.Setup(Alphaleonis.Win32.Filesystem.Path.GetPathRoot(ntdsFilePath));
                    string snap_path = vss.GetSnapshotPath(ntdsFilePath);

                    // Here we use the AlphaFS library to make the copy.
                    DirectoryInfo source = new DirectoryInfo(snap_path);
                    DirectoryInfo dest = new DirectoryInfo(destpath);
                    CopyFilesRecursively(source, dest);
                }
            }
            else
            {
                Console.WriteLine("Unable to find the path " + ntdsFilePath + ". Application should be run on a domain controller.");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }
        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                try
                {
                    file.CopyTo(Path.Combine(target.FullName, file.Name));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("Unable to copy a file - "+file.FullName);
                }
        }

        public static List<string> getHashes(string destpath, string keystr)
        {
            DirectoryContext.ValidateDatabaseState(destpath);
            var dir = new DirectoryContext(destpath, true);
            var agent = new DirectoryAgent(dir);
            var accounts = agent.GetAccounts(keystr.HexToBinary());
            List<string> accountNames = new List<string>();
            accountNames.Add("samAccountName;SID;LMHash;NTHash");
            foreach (DSAccount acc in accounts)
            {
                if (acc.Deleted == false && acc.Enabled == true && acc.SamAccountType.ToString() == "User")
                {
                    accountNames.Add(acc.SamAccountName.ToString() + ";"+acc.Sid.ToString() +";"+ acc.LMHash.ToHex() + ";" + acc.NTHash.ToHex());
                }
            }
            dir.Dispose();
            return accountNames;
        }
    }
}
