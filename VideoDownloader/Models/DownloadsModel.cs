using System;
using System.Collections.Generic;
using System.Linq;
using VideoDownloader.Services.Video;

namespace VideoDownloader.Models
{
    public class DownloadsModel
    {
        public DownloadsModel(IEnumerable<DownloadJob> downloadJobs)
        {
            JobProgresses = downloadJobs.Select(job => new JobProgress {Progress = (int) job.Progress, State = job.JobState, FriendlyState = job.JobState.ToString(), Title = job.Title, JobId = job.JobId});
        }

        public IEnumerable<JobProgress> JobProgresses { get; }

        public class JobProgress
        {
            public Guid JobId { get; set; }
            public int Progress { get; set; }
            public string Title { get; set; }
            public JobState State { get; set; }
            public string FriendlyState { get; set; }
        }
    }
}