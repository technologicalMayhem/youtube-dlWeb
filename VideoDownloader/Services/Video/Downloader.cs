using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;
using YoutubeDLSharp;

namespace VideoDownloader.Services.Video
{
    public class Downloader : IDownloader
    {
        private readonly ILogger<Downloader> _logger;
        private readonly IConfiguration _configuration;

        private readonly string _outputPath;
        private readonly List<DownloadJob> _jobs;
        private readonly string _saveLocation;

        /// <summary>
        /// Manages all the things that have to do with downloading Videos, converting them and making them available.
        /// </summary>
        public Downloader(ILogger<Downloader> logger, IConfiguration configuration)
        {
            _logger = logger;
            _outputPath = Path.Combine(Path.GetTempPath(), "videoDownloader");
            _configuration = configuration;
            _jobs = new List<DownloadJob>();
            _saveLocation = _configuration["SaveLocation"];

            Directory.CreateDirectory(_outputPath);
            foreach (var directory in Directory.GetDirectories(_outputPath))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(_saveLocation);
            if (!File.Exists("JobList.json")) return;
            var finishedJobs =
                JsonConvert.DeserializeObject<IEnumerable<DownloadJob>>(File.ReadAllText("JobList.json"));
            _jobs.AddRange(finishedJobs);
        }

        /// <summary>
        /// Returns a list of all jobs.
        /// </summary>
        /// <returns>List of all jobs.</returns>
        public IEnumerable<DownloadJob> GetJobs()
        {
            return _jobs;
        }

        /// <summary>
        /// Creates a new download job and runs it.
        /// </summary>
        /// <param name="videoUrl">The url of the video.</param>
        /// <returns>An object representing the current state of the download job.</returns>
        public async Task<DownloadJob> StartDownload(string videoUrl)
        {
            //Todo: Make the job fail if it's a duplicate. Maybe ask to overwrite instead?
            var job = new DownloadJob(videoUrl);
            job.StateChanged += JobOnStateChanged;
            await VerifyUrl(job);
            if (job.VerificationResult == VerificationResult.Valid)
            {
                _jobs.Add(job);
                job.Title = job.VideoData.Title;
                Task.Run(async () =>
                {
                    await DownloadVideo(job);
                    await ConvertVideo(job);
                    UploadVideo(job);
                    CleanupJob(job);
                });
            }

            return job;
        }

        /// <summary>
        /// Save all the jobs that are done to a file so if the application stops they can be restored.
        /// </summary>
        private void JobOnStateChanged(object sender, EventArgs e)
        {
            var finishedJobs = _jobs.Where(job => job.JobState == JobState.Done);
            File.WriteAllText("JobList.json", JsonConvert.SerializeObject(finishedJobs));
        }

        /// <summary>
        /// Deletes the file from the save location and deletes the associated job.
        /// </summary>
        /// <param name="id">The id of the job that should be deleted.</param>
        //Todo: Test if the delete actually works.
        public bool DeleteJob(string id)
        {
            var job = _jobs.First(listJob => listJob.JobId == Guid.Parse(id));
            try
            {
                File.Delete(job.SavePath);
            }
            catch (Exception e)
            {
                if (e is DirectoryNotFoundException || e is IOException)
                {
                    return false;
                }

                throw;
            }

            _jobs.Remove(job);
            return true;
        }

        /// <summary>
        /// Starts the the video download of a job.
        /// </summary>
        /// <param name="job">The job object that should be downloaded.</param>
        private async Task DownloadVideo(DownloadJob job)
        {
            job.JobState = JobState.Downloading;
            var dl = new YoutubeDL
            {
                OutputFolder = Path.Combine(_outputPath, job.JobId.ToString()),
                OutputFileTemplate = "%(title)s.%(ext)s"
            };
            var progress = new Progress<DownloadProgress>();
            progress.ProgressChanged += (sender, downloadProgress) => job.Progress = downloadProgress.Progress * 100;
            await dl.RunVideoDownload(job.Url, "bestvideo+bestaudio[format_id*=TV_Ton]/bestvideo+bestaudio/best",
                progress: progress);
        }

        /// <summary>
        /// Verifies if the url of a job is valid and adding the result to it.
        /// </summary>
        /// <param name="job">The job object that should be verified.</param>
        private async Task VerifyUrl(DownloadJob job)
        {
            var dl = new YoutubeDL();
            var result = await dl.RunVideoDataFetch(job.Url);

            if (result.Data == null)
            {
                if (result.ErrorOutput.Contains("Forbidden"))
                {
                    job.VerificationResult = VerificationResult.DrmProtected;
                    return;
                }

                if (result.ErrorOutput.Contains("Unsupported Url"))
                {
                    job.VerificationResult = VerificationResult.NoVideoFound;
                    return;
                }

                job.VerificationResult = VerificationResult.GenericError;
                return;
            }

            if (result.Data.Entries != null)
            {
                job.VerificationResult = VerificationResult.IsPlaylist;
                return;
            }

            job.VideoData = result.Data;
            job.VerificationResult = VerificationResult.Valid;
        }

        /// <summary>
        /// Convert the video to a mkv file.
        /// </summary>
        /// <param name="job">The job object that should be converted.</param>
        private async Task ConvertVideo(DownloadJob job)
        {
            job.JobState = JobState.Converting;
            var inputFilePath = Directory.GetFiles(Path.Combine(_outputPath, job.JobId.ToString())).First();
            //If the file is already a mkv, just stop.
            if (Path.GetExtension(inputFilePath) == FileExtensions.Mkv) return;
            var outputFilePath = Path.Combine(_outputPath, job.JobId.ToString(),
                Path.ChangeExtension(inputFilePath, FileExtensions.Mkv));

            var conversion = Conversion.Convert(inputFilePath, outputFilePath);
            conversion.AddParameter("-codec copy");
            conversion.OnProgress += (sender, args) => job.Progress = args.Percent;
            await conversion.Start();
            File.Delete(inputFilePath);
        }

        /// <summary>
        /// Moves the finished video file to the save location.
        /// </summary>
        /// <param name="job">The job to move to the save location.</param>
        private void UploadVideo(DownloadJob job)
        {
            job.JobState = JobState.Uploading;
            var videoFile = Directory.GetFiles(Path.Combine(_outputPath, job.JobId.ToString())).First();
            job.SavePath = Path.Combine(_saveLocation, Path.GetFileName(videoFile));
            File.Copy(videoFile, job.SavePath, true);
        }

        /// <summary>
        /// CLeans up the working files of the job and marks the job as finished.
        /// </summary>
        /// <param name="job">The job that should be cleaned up.</param>
        private void CleanupJob(DownloadJob job)
        {
            job.Progress = 100;
            job.JobState = JobState.Done;
            Directory.Delete(Path.Combine(_outputPath, job.JobId.ToString()), true);
        }
    }
}