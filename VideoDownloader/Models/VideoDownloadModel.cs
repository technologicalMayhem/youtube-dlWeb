using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace VideoDownloader.Models
{
    public class VideoDownloadModel
    {
        [Url]
        [DisplayName("Video Url")]
        public string VideoUrl { get; set; }

        public bool IsValid { get; set; } = true;
        public string Message { get; set; }
    }
}