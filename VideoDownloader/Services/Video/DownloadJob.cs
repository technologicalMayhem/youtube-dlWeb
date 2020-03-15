using System;
using System.Security.Policy;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;

namespace VideoDownloader.Services.Video
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
        Done
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