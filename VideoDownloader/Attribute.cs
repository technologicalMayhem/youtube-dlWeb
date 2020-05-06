using System;

namespace VideoDownloader
{
    public class DownloaderDefinition : Attribute
    {
        /// <summary>
        /// A regular expression to check if the downloader can utilize this link.
        /// </summary>
        public string WebsiteUrl;
    }
}