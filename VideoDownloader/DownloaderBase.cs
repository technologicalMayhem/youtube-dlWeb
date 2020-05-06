using System;
using System.Threading.Tasks;

namespace VideoDownloader
{
    public abstract class DownloaderBase
    {
        public static bool HasProgressBar;
        public Progress<float> Progress;

        protected virtual Task<DownloadResult> StartDownload(string url)
        {
            throw new NotImplementedException();
        }
    }
}