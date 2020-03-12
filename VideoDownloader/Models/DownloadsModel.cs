using System.Collections.Generic;
using VideoDownloader.Services;

namespace VideoDownloader.Models
{
    public class DownloadsModel
    {
        public IEnumerable<DownloadFileInfo> DownloadInfos { get; set; }
    }
}