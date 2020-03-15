using System.Collections.Generic;
using System.Threading.Tasks;

namespace VideoDownloader.Services.Video
{
    public interface IDownloader
    {
        public IEnumerable<DownloadJob> GetJobs();
        public Task<DownloadJob> StartDownload(string videoUrl);
        public bool DeleteJob(string id);
    }
}