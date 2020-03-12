using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using NYoutubeDL;
using NYoutubeDL.Models;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;

namespace VideoDownloader.Services
{
    public class VideoDownloader : IVideoDownloader
    {
        private readonly ILogger<VideoDownloader> _logger;
        private readonly Dictionary<string, DownloadFileInfo> _downloadInfos;

        public VideoDownloader(ILogger<VideoDownloader> logger)
        {
            _logger = logger;
            _downloadInfos = new Dictionary<string, DownloadFileInfo>();
        }

        public Task DownloadVideo(string videoUrl)
        {
            var task = Task.Factory.StartNew(() =>
            {
                Directory.CreateDirectory("logs");
                Directory.CreateDirectory("output");
                _logger.Log(LogLevel.Information, $"Downloading: {videoUrl}");

                var dl = new YoutubeDL();
                var id = Guid.NewGuid().ToString();
                var downloadFileInfo = new DownloadFileInfo();
                _downloadInfos.Add(id, downloadFileInfo);

                dl.Options.VideoFormatOptions.FormatAdvanced = "bestvideo+bestaudio[format_id *= TV_Ton]/best";
                dl.Options.FilesystemOptions.Output = Path.Combine("output", id, "%(title)s.%(ext)s");
                dl.VideoUrl = videoUrl;
                DownloadInfo downloadInfo;
                try
                {
                    downloadInfo = dl.GetDownloadInfo();
                    _downloadInfos[id].Title = dl.GetDownloadInfo().Title;
                }
                catch (Exception e)
                {
                    _logger.LogError($"{videoUrl} is not a valid url.");
                    return;
                }
                
                var logFile = Path.Combine("logs", $"{DateTime.Now:yy-MM-dd.hh-mm-ss} - {downloadInfo.Title}.log");
                var logFileWriter = File.AppendText(logFile);
                dl.StandardOutputEvent += (sender, s) => logFileWriter.WriteLine(s);
                dl.StandardErrorEvent += (sender, s) => logFileWriter.WriteLine(s);

                var downloadAsync = dl.DownloadAsync();
                _downloadInfos[id].DownloadStage = DownloadStage.Downloading;
                while (!downloadAsync.IsCompleted)
                {
                    if (_downloadInfos[id].Progress < dl.Info.VideoProgress / 10)
                    {
                        _downloadInfos[id].Progress = dl.Info.VideoProgress / 10;
                    }
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }

                _downloadInfos[id].Progress = 100;
                _logger.Log(LogLevel.Information, $"Download of {videoUrl} completed.");
                ConvertToMkv(id);
            });
            return task;
        }

        private void ConvertToMkv(string key)
        {
            var inputFilePath = Directory.GetFiles(Path.Combine("output", key)).First();
            var outputFilePath = Path.ChangeExtension(inputFilePath, FileExtensions.Mkv);
            _logger.Log(LogLevel.Information,
                $"Starting conversion of {Path.GetFileName(inputFilePath)} to {Path.GetFileName(outputFilePath)}.");
            var conversion = Conversion.Convert(inputFilePath, outputFilePath);
            conversion.AddParameter("-codec copy");
            conversion.OnProgress += (sender, args) => _downloadInfos[key].Progress = args.Percent;
            _downloadInfos[key].DownloadStage = DownloadStage.Converting;
            conversion.Start()
                .Wait();
            _downloadInfos[key].DownloadStage = DownloadStage.Done;
            _logger.Log(LogLevel.Information,
                $"Finished conversion of {Path.GetFileName(inputFilePath)} to {Path.GetFileName(outputFilePath)}.");
            File.Delete(inputFilePath);
        }

        public IEnumerable<DownloadFileInfo> GetDownloadInfo()
        {
            return _downloadInfos.Values;
        }
    }

    public class DownloadFileInfo
    {
        public DownloadStage DownloadStage { get; set; } = DownloadStage.Preparing;
        public string Title { get; set; }
        public int Progress { get; set; }
    }

    public enum DownloadStage
    {
        Preparing,
        Downloading,
        Converting,
        Done
    }

    public interface IVideoDownloader
    {
        Task DownloadVideo(string videoUrl);
        IEnumerable<DownloadFileInfo> GetDownloadInfo();
    }
}