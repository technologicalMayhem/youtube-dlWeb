using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;
using YoutubeDLSharp;

namespace VideoDownloader
{
    public class Downloader : IDisposable
    {
        //Todo: Have jobs save their status and be resumable

        private readonly ILogger<DownloaderBase> _logger;

        private readonly string _workingDirectory;
        private readonly List<DownloadJob> _jobs;
        private readonly string _saveLocation;

        /// <summary>
        /// Manages all the things that have to do with downloading Videos, converting them and making them available.
        /// </summary>
        public Downloader(DownloaderConfiguration configuration)
        {
            var factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });
            _logger = factory.CreateLogger<DownloaderBase>();

            _workingDirectory = string.IsNullOrEmpty(configuration.WorkDirectory)
                ? Path.Combine(Path.GetTempPath(), "VideoDownloader")
                : configuration.WorkDirectory;
            _saveLocation = string.IsNullOrEmpty(configuration.SaveDirectory)
                ? throw new Exception("The save location has not been set or is invalid.")
                : configuration.SaveDirectory;

            Directory.CreateDirectory(_workingDirectory);
            Directory.CreateDirectory(_saveLocation);

            _jobs = new List<DownloadJob>();
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
                job.CancellationTokenSource = new CancellationTokenSource();
                _jobs.Add(job);
                job.Title = job.VideoData.Title;
                var task = Task.Run(async () =>
                {
                    if (!job.CancellationTokenSource.IsCancellationRequested)
                        await DownloadVideo(job, job.CancellationTokenSource.Token);
                    if (!job.CancellationTokenSource.IsCancellationRequested)
                        await ConvertVideo(job, job.CancellationTokenSource.Token);
                    if (!job.CancellationTokenSource.IsCancellationRequested)
                        await UploadVideo(job, job.CancellationTokenSource.Token);

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
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "JobList.json"),
                JsonConvert.SerializeObject(finishedJobs));
        }

        /// <summary>
        /// Deletes the file from the save location and deletes the associated job.
        /// </summary>
        /// <param name="id">The id of the job that should be deleted.</param>
        //Todo: Test if the delete actually works.
        public void DeleteJob(string id)
        {
            var job = _jobs.First(listJob => listJob.JobId == Guid.Parse(id));
            if (job.JobState != JobState.Done)
            {
                job.CancellationTokenSource.Cancel();
                if (job.JobState == JobState.Uploading)
                {
                    SpinWait.SpinUntil(() => IsFileLocked(job.SavePath), TimeSpan.FromMinutes(1));
                }
            }

            ;
            try
            {
                File.Delete(job.SavePath);
            }
            catch (IOException e)
            {
                _logger.LogError($"Job {job.SavePath} save file could not be deleted.\n{e}");
            }

            _jobs.RemoveAll(downloadJob => downloadJob.JobId == job.JobId);
            JobOnStateChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Verifies if the url of a job is valid and adding the result to it.
        /// </summary>
        /// <param name="job">The job object that should be verified.</param>
        private static async Task VerifyUrl(DownloadJob job)
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
        /// Starts the the video download of a job.
        /// </summary>
        /// <param name="job">The job object that should be downloaded.</param>
        /// <param name="token">A cancellation token.</param>
        private async Task DownloadVideo(DownloadJob job, CancellationToken token)
        {
            job.JobState = JobState.Downloading;
            var dl = new YoutubeDL
            {
                OutputFolder = Path.Combine(_workingDirectory, job.JobId.ToString()),
                OutputFileTemplate = "%(title)s.%(ext)s"
            };
            var progress = new Progress<DownloadProgress>();
            progress.ProgressChanged += (sender, downloadProgress) => job.Progress = downloadProgress.Progress * 100;
            await dl.RunVideoDownload(job.Url, "bestvideo+bestaudio[format_id*=TV_Ton]/bestvideo+bestaudio/best",
                progress: progress, ct: token);
        }

        /// <summary>
        /// Convert the video to a mkv file.
        /// </summary>
        /// <param name="job">The job object that should be converted.</param>
        /// <param name="cancellationTokenToken"></param>
        private async Task ConvertVideo(DownloadJob job, CancellationToken cancellationTokenToken)
        {
            job.JobState = JobState.Converting;
            var inputFilePath = Directory.GetFiles(Path.Combine(_workingDirectory, job.JobId.ToString())).First();
            //If the file is already a mkv, just stop.
            if (Path.GetExtension(inputFilePath) == FileExtensions.Mkv) return;
            var outputFilePath = Path.Combine(_workingDirectory, job.JobId.ToString(),
                Path.ChangeExtension(inputFilePath, FileExtensions.Mkv));

            var conversion = Conversion.Convert(inputFilePath, outputFilePath);
            conversion.AddParameter("-codec copy");
            conversion.OnProgress += (sender, args) => job.Progress = args.Percent;
            await conversion.Start(cancellationTokenToken);
            File.Delete(inputFilePath);
        }

        /// <summary>
        /// Moves the finished video file to the save location.
        /// </summary>
        /// <param name="job">The job to move to the save location.</param>
        /// <param name="token"></param>
        private async Task UploadVideo(DownloadJob job, CancellationToken token)
        {
            job.JobState = JobState.Uploading;
            var sourceFile = Directory.GetFiles(Path.Combine(_workingDirectory, job.JobId.ToString())).First();
            job.SavePath = Path.Combine(_saveLocation, Path.GetFileName(sourceFile));
            await CopyFileAsync(sourceFile, job.SavePath, token);
        }

        /// <summary>
        /// CLeans up the working files of the job and marks the job as finished.
        /// </summary>
        /// <param name="job">The job that should be cleaned up.</param>
        private void CleanupJob(DownloadJob job)
        {
            job.Progress = 100;
            job.JobState = JobState.Done;
            job.CancellationTokenSource.Dispose();
            Directory.Delete(Path.Combine(_workingDirectory, job.JobId.ToString()), true);
        }

        /// <summary>
        /// Copies files asynchronously. Can be stopped using a cancellation token. 
        /// </summary>
        /// <param name="sourcePath">The file that to copy.</param>
        /// <param name="destinationPath">The Path to the destination file.</param>
        /// <param name="token">A cancellation token.</param>
        private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            await using Stream source = File.Open(sourcePath, FileMode.Open);
            await using Stream destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, token);
        }

        /// <summary>
        /// Check if the file is currently in use or being written to.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>Whether or not the file is currently in use.</returns>
        private static bool IsFileLocked(string path)
        {
            var file = new FileInfo(path);
            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            
        }
    }
}