using System;
using System.Threading;
using YoutubeDLSharp.Metadata;

namespace VideoDownloader
{
    public class DownloadJob
    {
        private JobState _jobState;
        
        public Guid JobId { get; }
        public string Url { get; }
        public string Title { get; set; }
        public double Progress { get; set; }
        public VideoData VideoData { get; set; }
        public VerificationResult VerificationResult { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }

        public JobState JobState
        {
            get => _jobState;
            set
            {
                _jobState = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string SavePath { get; set; }

        public event EventHandler StateChanged;
        
        public DownloadJob(string videoUrl)
        {
            JobId = Guid.NewGuid();
            Url = videoUrl;
            JobState = JobState.Preparing;
        }
    }

    public enum JobState
    {
        Preparing,
        Downloading,
        Converting,
        Uploading,
        Done,
        Dispose
    }

    public enum VerificationResult
    {
        Valid,
        IsPlaylist,
        NoVideoFound,
        DrmProtected,
        GenericError
    }
}