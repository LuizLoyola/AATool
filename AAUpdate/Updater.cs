﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace AAUpdate
{
    public delegate void StatusEventHandler(int key, string value);
    public delegate void ProgressEventHandler(int key, (int, int) value);

    public class Updater
    {
        //keys to refer to specific labels/progress bars on main window
        public const int STATUS_1   = 0;
        public const int STATUS_2   = 1;
        public const int STATUS_3   = 2;
        public const int PROGRESS_1 = 0;
        public const int PROGRESS_2 = 1;

        private const string PROGRAM_NAME  = "AATool";
        private const string ZIP_EXTENSION = ".zip";
        private const string URL_GITHUB    = "https://github.com/";
        private const string URL_REPO      = "DarwinBaker/AATool/";
        private const string URL_LATEST    = URL_REPO + "releases/latest/";
        private const string URL_DOWNLOAD  = URL_REPO + "releases/download/";

        public DirectoryInfo Source      { get; private set; }
        public DirectoryInfo Destination { get; private set; }
        public FileInfo TempZip          { get; private set; }
        public HashSet<string> NewFiles  { get; private set; }
        public HashSet<string> OldFiles  { get; private set; }
        public string LatestVersion      { get; private set; }

        public bool StartAAToolWhenDone;

        //events to send ui updates to main thread
        public event StatusEventHandler StatusChanged;
        public event ProgressEventHandler ProgressChanged;

        public string TempToolFolder            => Path.Combine(Path.GetTempPath(), PROGRAM_NAME);
        public string TempUpdaterFolder         => Path.Combine(Path.GetTempPath(), Process.GetCurrentProcess().ProcessName);
        public string TempUpdaterExecutable     => Path.Combine(Path.GetTempPath(), Process.GetCurrentProcess().ProcessName, AppDomain.CurrentDomain.FriendlyName);
        public void SetDestination(string path) => Destination = new DirectoryInfo(path);

        public bool FileContentsEqual(FileInfo a, FileInfo b)
        {
            //compare two files on binary level
            if (a.Length != b.Length)
                return false;
            if (a.Length == 0)
                return false;
            if (!File.ReadAllBytes(a.FullName).SequenceEqual(File.ReadAllBytes(b.FullName)))
                return false;
            return true;
        }

        public HashSet<string> GetFilesRecursive(DirectoryInfo directory)
        {
            //recursively build and return a hashset containing all files in all sub-folders of a directory
            var files = new HashSet<string>();
            foreach (var file in directory.GetFiles("*.*", SearchOption.AllDirectories))
                if (!file.DirectoryName.Contains("settings"))
                    files.Add(file.FullName.Replace(directory.FullName, "").Trim(new char[] { '\\', '/' }));
            return files;
        }

        public void CloneToTempFolder()
        {           
            if (!Directory.Exists(TempUpdaterFolder))
                Directory.CreateDirectory(TempUpdaterFolder);

            string name = AppDomain.CurrentDomain.FriendlyName;
            string tempExecutable = Path.Combine(TempUpdaterFolder, name);

            //copy this executable to temp folder
            File.Copy(AppDomain.CurrentDomain.FriendlyName, tempExecutable, true);
        }

        public void TryUpdate()
        {
            //download and extract latest release
            ProgressChanged(PROGRESS_1, (1, 4));
            DownloadLatestZip();
            ProgressChanged(PROGRESS_1, (2, 4));
            ExtractLatestZip();

            //compare latest release against current install
            ProgressChanged(PROGRESS_1, (3, 4));
            if (VerifyInstallation())
            {
                //all files match and everything is up to date
                StatusChanged(STATUS_1, "You already have the lastest version (" + LatestVersion + ") of CTM's AATool.");
                StatusChanged(STATUS_2, "Installation verified!");
            }
            else
            {
                //current install isn't up to date or is corrupted; replace missing/outdated files
                StatusChanged(STATUS_1, "Updating...");
                MirrorSourceToDestination();
                StatusChanged(STATUS_3, "");

                if (VerifyInstallation())
                {
                    //all files match and everything is up to date
                    ProgressChanged(PROGRESS_2, (0, 100));
                    ProgressChanged(PROGRESS_2, (100, 100));
                    StatusChanged(STATUS_1, "CTM's AATool succesfully updated to version " + LatestVersion + "!");
                    StatusChanged(STATUS_2, "Installation verified!");
                }
                else
                {
                    //files still don't match, os is probably locking a file
                    ProgressChanged(PROGRESS_2, (0, 100));
                    ProgressChanged(PROGRESS_2, (100, 100));
                    StatusChanged(STATUS_1, "Update to version " + LatestVersion + " may have failed!");
                    StatusChanged(STATUS_2, "Installation couldn't be verified.");
                    StatusChanged(STATUS_3, "Try restarting your PC then updating again, or manually re-install.");
                }
            }
            ProgressChanged(PROGRESS_1, (4, 4));
        }

        private void DownloadLatestZip()
        {
            using (var client = new WebClient())
            {
                ProgressChanged(PROGRESS_2, (0, 100));
                ProgressChanged(PROGRESS_2, (100, 100));
                StatusChanged(STATUS_1, "Checking for updates...");
                StatusChanged(STATUS_2, "Parsing GitHub releases...");

                //get latest github release page
                string html = client.DownloadString(URL_GITHUB + URL_LATEST);
                int start   = html.IndexOf(URL_DOWNLOAD);
                int end     = html.IndexOf(ZIP_EXTENSION);

                //parse zip download link from release page
                string zipLink   = "https://github.com/" + html.Substring(start, end - start) + ZIP_EXTENSION; ;
                string zipName   = zipLink.Substring(zipLink.LastIndexOf('/') + 1);
                string zipFile   = Path.Combine(TempToolFolder, zipName);
                string zipFolder = Path.Combine(TempToolFolder, zipFile.Remove(zipFile.IndexOf(ZIP_EXTENSION)));
                Source           = new DirectoryInfo(zipFolder);
                LatestVersion    = zipFile.Substring(zipFile.LastIndexOf('_') + 1).Replace(".zip", "");

                //delete and re-create temp folder to download and extract new version to
                if (Directory.Exists(TempToolFolder))
                    Directory.Delete(TempToolFolder, true);
                if (!Directory.Exists(TempToolFolder))
                    Directory.CreateDirectory(TempToolFolder);

                ProgressChanged(PROGRESS_2, (0, 100));
                ProgressChanged(PROGRESS_2, (100, 100));
                StatusChanged(STATUS_2, "Downloading latest release (" + LatestVersion + ")  from GitHub...");

                //download release zip to temp folder
                TempZip = new FileInfo(Path.Combine(TempToolFolder, zipName));
                client.DownloadFile(zipLink, TempZip.FullName);   
            }
        }

        private void ExtractLatestZip()
        {
            ProgressChanged(PROGRESS_2, (0, 100));
            ProgressChanged(PROGRESS_2, (100, 100));
            StatusChanged(STATUS_2, "Extracting latest release to temporary location...");

            //extract release zip
            ZipFile.ExtractToDirectory(TempZip.FullName, TempToolFolder);

            ProgressChanged(PROGRESS_2, (0, 100));
            ProgressChanged(PROGRESS_2, (100, 100));
            StatusChanged(STATUS_2, "Compiling file lists...");

            //populate lists of files from source and destination to compare diffs
            OldFiles = GetFilesRecursive(Destination);
            NewFiles = GetFilesRecursive(Source);
        }

        public bool VerifyInstallation()
        {
            ProgressChanged(PROGRESS_2, (0, 100));
            ProgressChanged(PROGRESS_2, (100, 100));
            StatusChanged(STATUS_2, "Verifying current installation...");

            //compare binary contents of all files
            foreach (var file in NewFiles)
            {
                var destinationInfo = new FileInfo(Path.Combine(Destination.FullName, file));
                var sourceInfo = new FileInfo(Path.Combine(Source.FullName, file));
                if (!destinationInfo.Exists || !FileContentsEqual(destinationInfo, sourceInfo))
                    return false;
            }
            return true;
        }

        public void DeleteEmptyDirectories(DirectoryInfo directory)
        {
            //recursively delete all empty folders in a directory
            foreach (var subDirectory in directory.GetDirectories())
            {
                DeleteEmptyDirectories(subDirectory);
                if (subDirectory.GetFiles().Length == 0 && subDirectory.GetDirectories().Length == 0)
                    subDirectory.Delete(false);
            }
        }

        private void MirrorSourceToDestination()
        {
            try
            {
                StatusChanged(STATUS_2, "Deleting depricated files...");
                int processed = 0;
                foreach (var file in OldFiles)
                {
                    processed++;
                    ProgressChanged(PROGRESS_2, (processed, OldFiles.Count));

                    //if file from current install isn't in latest release delete it
                    if (!NewFiles.Contains(file))
                    {
                        StatusChanged(STATUS_3, file);
                        File.Delete(Path.Combine(Destination.FullName, file));
                    }
                }
                //clear out any empty directories
                DeleteEmptyDirectories(Destination);

                StatusChanged(STATUS_2, "Copying updated files...");
                processed = 0;
                foreach (var file in NewFiles)
                {
                    processed++;
                    ProgressChanged(PROGRESS_2, (processed, NewFiles.Count));

                    //if file from latest release isn't in current install or is different replace it
                    var oldInfo = new FileInfo(Path.Combine(Destination.FullName, file));
                    var newInfo = new FileInfo(Path.Combine(Source.FullName, file));
                    if (!oldInfo.Exists || !FileContentsEqual(oldInfo, newInfo))
                    {
                        StatusChanged(STATUS_3, file);
                        oldInfo.Directory.Create();
                        newInfo.CopyTo(oldInfo.FullName, true);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
