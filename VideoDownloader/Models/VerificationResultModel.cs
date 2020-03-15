namespace VideoDownloader.Models
{
    public class VerificationResultModel
    {
        public VerificationResultModel(bool isDownloading, string message)
        {
            IsDownloading = isDownloading;
            Message = message;
        }

        public bool IsDownloading { get; }
        public string Message { get; }
    }
}