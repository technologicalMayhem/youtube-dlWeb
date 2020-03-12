using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VideoDownloader.Models;
using VideoDownloader.Services;

namespace VideoDownloader.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IVideoDownloader _videoDownloader;

        public HomeController(ILogger<HomeController> logger, IVideoDownloader videoDownloader)
        {
            _logger = logger;
            _videoDownloader = videoDownloader;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new VideoDownloadModel();
            return View(model);
        }

        [HttpPost]
        public IActionResult Index(VideoDownloadModel model)
        {
            _videoDownloader.DownloadVideo(model.VideoUrl);
            ViewBag.Message = "Download begonnen.";
            model.VideoUrl = "";
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        public IActionResult Downloads()
        {
            var model = new DownloadsModel{DownloadInfos = _videoDownloader.GetDownloadInfo()};
            return View(model);
        }
    }
}