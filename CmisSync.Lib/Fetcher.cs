﻿//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using log4net;
using CmisSync.Lib.Cmis;
using CmisSync.Lib.Sync;

namespace CmisSync.Lib
{

    public class Fetcher
    {
        CmisRepo CmisRepo;

        protected static readonly ILog Logger = LogManager.GetLogger(typeof(Fetcher));

        public event Action Started = delegate { };
        public event Action Failed = delegate { };

        public event FinishedEventHandler Finished = delegate { };
        public delegate void FinishedEventHandler(bool repo_is_encrypted, bool repo_is_empty, string[] warnings);

        public event ProgressChangedEventHandler ProgressChanged = delegate { };
        public delegate void ProgressChangedEventHandler(double percentage);

        public Uri RemoteUrl { get; protected set; }
        public string TargetFolder { get; protected set; }
        public bool IsActive { get; private set; }
        public string Identifier;
        public RepoInfo OriginalRepoInfo;

        public string[] Warnings
        {
            get
            {
                return this.warnings.ToArray();
            }
        }

        public string[] Errors
        {
            get
            {
                return this.errors.ToArray();
            }
        }


        protected List<string> warnings = new List<string>();
        protected List<string> errors = new List<string>();

        protected string[] ExcludeRules = new string[] {
            "*.autosave", // Various autosaving apps
            "*~", // gedit and emacs
            ".~lock.*", // LibreOffice
            "*.part", "*.crdownload", // Firefox and Chromium temporary download files
            ".*.sw[a-z]", "*.un~", "*.swp", "*.swo", // vi(m)
            ".directory", // KDE
            ".DS_Store", "Icon\r", "._*", ".Spotlight-V100", ".Trashes", // Mac OS X
            "*(Autosaved).graffle", // Omnigraffle
            "Thumbs.db", "Desktop.ini", // Windows
            "~*.tmp", "~*.TMP", "*~*.tmp", "*~*.TMP", // MS Office
            "~*.ppt", "~*.PPT", "~*.pptx", "~*.PPTX",
            "~*.xls", "~*.XLS", "~*.xlsx", "~*.XLSX",
            "~*.doc", "~*.DOC", "~*.docx", "~*.DOCX",
            "*/CVS/*", ".cvsignore", "*/.cvsignore", // CVS
            "/.svn/*", "*/.svn/*", // Subversion
            "/.hg/*", "*/.hg/*", "*/.hgignore", // Mercurial
            "/.bzr/*", "*/.bzr/*", "*/.bzrignore" // Bazaar
        };


        private Thread thread;


        // Sets up a fetcher that can get remote folders
        public Fetcher(RepoInfo repoInfo, ActivityListener activityListener)
        {
            OriginalRepoInfo = repoInfo;
            string remote_path = repoInfo.RemotePath.Trim("/".ToCharArray());
            string address = repoInfo.Address.ToString();

            if (address.EndsWith("/"))
                address = address.Substring(0, address.Length - 1);

            if (!remote_path.StartsWith("/"))
                remote_path = "/" + remote_path;

            if (!address.Contains("://"))
                address = "ssh://" + address;

            TargetFolder = repoInfo.TargetDirectory;

            RemoteUrl = new Uri(address + remote_path);
            IsActive = false;

            Logger.Info("Fetcher | Cmis Fetcher constructor");
            TargetFolder = repoInfo.TargetDirectory;
            RemoteUrl = repoInfo.Address;

            if (!Directory.Exists(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} do not exist", ConfigManager.CurrentConfig.FoldersPath));
                throw new DirectoryNotFoundException("Root folder don't exist !");
            }

            if (!Folder.HasWritePermissionOnDir(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} is not writable", ConfigManager.CurrentConfig.FoldersPath));
                throw new UnauthorizedAccessException("Root folder is not writable!");
            }

            if (Directory.Exists(repoInfo.TargetDirectory))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Repository Folder {0} already exist", repoInfo.TargetDirectory));
                throw new UnauthorizedAccessException("Repository folder already exists!");
            }

            Directory.CreateDirectory(repoInfo.TargetDirectory);

            CmisRepo = new CmisRepo(repoInfo, activityListener);
        }


        public bool Fetch()
        {
            Logger.Info("Fetch");
            CmisRepo.DoFirstSync();
            return true; // TODO
        }


        // TODO used?
        public void Start()
        {
            IsActive = true;
            Started();

            Logger.Info("Fetcher | " + TargetFolder + " | Fetching folder: " + RemoteUrl);

            this.thread = new Thread(() =>
            {
                if (Fetch())
                {
                    Thread.Sleep(500);
                    Logger.Info("Fetcher | " + OriginalRepoInfo.Name + " | Finished");

                    IsActive = false;

                    bool repo_is_encrypted = (RemoteUrl.AbsolutePath.Contains("-crypto") ||
                                              RemoteUrl.Host.Equals("CmisSync.com"));

                    Finished(repo_is_encrypted, false, Warnings);
                }
                else
                {
                    Thread.Sleep(500);
                    Logger.Warn("Fetcher | " + OriginalRepoInfo.Name + " | Failed");

                    IsActive = false;
                    Failed();
                }
            });

            this.thread.Start();
        }


        public void Dispose()
        {
            if (this.thread != null)
                this.thread.Abort();
        }

        // TODO remove, not used
        protected void OnProgressChanged(double percentage)
        {
            ProgressChanged(percentage);
        }
    }
}
