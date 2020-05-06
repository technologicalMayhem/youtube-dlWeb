using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VideoDownloader
{
    public class DownloaderConfiguration
    {
        public DownloaderConfiguration(string saveDirectory)
        {
            SaveDirectory = saveDirectory;
        }

        public DownloaderConfiguration()
        {
            
        }

        /// <summary>
        /// Required. Defines the path that finished files are being saved to.
        /// </summary>
        public string SaveDirectory { get; set; }

        /// <summary>
        /// Optional. Defines the path for the directory that will be used to save intermediary files to.
        /// </summary>
        public string WorkDirectory { get; set; }

        public bool Debug { get; set; } = false;
    }
}