using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VideoDownloader.Models;
using VideoDownloader.Services;
using VideoDownloader.Services.Video;

namespace VideoDownloader.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IDownloader _downloader;

        public HomeController(ILogger<HomeController> logger, IDownloader downloader)
        {
            _logger = logger;
            _downloader = downloader;
        }
        
        [HttpGet]
        public IActionResult Index()
        {
            var model = new VideoDownloadModel();
            return View(model);
        }
        
        [HttpPost]
        public async Task<IActionResult> Index(VideoDownloadModel model)
        {
            var result = await _downloader.StartDownload(model.VideoUrl);
            model.IsValid = result.VerificationResult == VerificationResult.Valid;
            model.Message = result.VerificationResult.ToString();
            if (result.VerificationResult == VerificationResult.Valid)
            {
                return Redirect("/Downloads");
            }
            return View(model);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        public IActionResult Downloads(bool json)
        {
            var model = new DownloadsModel(_downloader.GetJobs());
            if (json)
            {
                return Json(model);
            }
            return View(model);
        }

        public bool RemoveVideo(string id)
        {
            return _downloader.DeleteJob(id);
        }
    }
}