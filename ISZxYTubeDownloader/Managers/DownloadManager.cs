using ISZxYTubeDownloader;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace ISZxYTubeDownloader
{
    /// <summary>
    /// Enhanced download manager with retry logic and better error handling
    /// </summary>
    public class DownloadManager
    {
        private readonly YoutubeClient _youtubeClient;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;

        public DownloadManager(int maxRetries = 3, int retryDelayMs = 2000)
        {
            _youtubeClient = new YoutubeClient();
            _maxRetries = maxRetries;
            _retryDelayMs = retryDelayMs;
        }

        /// <summary>
        /// Download a video with retry logic
        /// </summary>
        public async Task<DownloadResult> DownloadVideoAsync(
            DownloadItem item,
            string outputPath,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            int retryCount = 0;
            Exception lastException = null;

            while (retryCount <= _maxRetries)
            {
                try
                {
                    return await DownloadVideoInternalAsync(
                        item, outputPath, progress, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new DownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Download cancelled by user",
                        IsCancelled = true
                    };
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;

                    if (retryCount <= _maxRetries)
                    {
                        await Task.Delay(_retryDelayMs, cancellationToken);
                    }
                }
            }

            return new DownloadResult
            {
                Success = false,
                ErrorMessage = $"Failed after {_maxRetries} retries: {lastException?.Message}",
                Exception = lastException
            };
        }

        private async Task<DownloadResult> DownloadVideoInternalAsync(
            DownloadItem item,
            string outputPath,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            // Get video metadata
            var video = await _youtubeClient.Videos.GetAsync(item.Url);
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);

            // Sanitize filename
            string fileName = SanitizeFileName(video.Title);
            string filePath;
            IStreamInfo streamInfo;

            // Select appropriate stream
            switch (item.Format)
            {
                case "audio":
                    streamInfo = GetBestAudioStream(streamManifest, item.Quality);
                    if (streamInfo == null)
                        return new DownloadResult { Success = false, ErrorMessage = "No audio stream found" };
                    filePath = Path.Combine(outputPath, $"{fileName}.{streamInfo.Container}");
                    break;

                case "video":
                    streamInfo = GetVideoStream(streamManifest, item.Quality);
                    if (streamInfo == null)
                        return new DownloadResult { Success = false, ErrorMessage = "No video stream found" };
                    filePath = Path.Combine(outputPath, $"{fileName}_video.{streamInfo.Container}");
                    break;

                case "muxed":
                default:
                    streamInfo = GetMuxedStream(streamManifest, item.Quality);
                    if (streamInfo == null)
                        return new DownloadResult { Success = false, ErrorMessage = "No suitable stream found" };
                    filePath = Path.Combine(outputPath, $"{fileName}.{streamInfo.Container}");
                    break;
            }

            // Download the stream
            await _youtubeClient.Videos.Streams.DownloadAsync(
                streamInfo,
                filePath,
                progress,
                cancellationToken);

            return new DownloadResult
            {
                Success = true,
                FilePath = filePath,
                FileSize = streamInfo.Size.Bytes,
                VideoTitle = video.Title,
                Duration = video.Duration?.TotalSeconds ?? 0
            };
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars));

            // Limit length to avoid path too long errors
            if (sanitized.Length > 200)
                sanitized = sanitized.Substring(0, 200);

            return sanitized;
        }

        private MuxedStreamInfo GetMuxedStream(StreamManifest manifest, string quality)
        {
            var muxedStreams = manifest.GetMuxedStreams().ToList();

            switch (quality)
            {
                case "1080":
                    return muxedStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 1080)
                        ?? muxedStreams.OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                case "720":
                    return muxedStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 720)
                        ?? muxedStreams.OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                case "480":
                    return muxedStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 480)
                        ?? muxedStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 360);
                case "360":
                    return muxedStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 360)
                        ?? muxedStreams.OrderBy(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                case "lowest":
                    return muxedStreams.OrderBy(s => s.Size.Bytes).FirstOrDefault();
                case "best":
                default:
                    return muxedStreams.OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
            }
        }

        private VideoOnlyStreamInfo GetVideoStream(StreamManifest manifest, string quality)
        {
            var videoStreams = manifest.GetVideoOnlyStreams().ToList();

            switch (quality)
            {
                case "1080":
                    return videoStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 1080)
                        ?? videoStreams.OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                case "720":
                    return videoStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 720)
                        ?? videoStreams.OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                case "480":
                    return videoStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 480)
                        ?? videoStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 360);
                case "360":
                    return videoStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == 360)
                        ?? videoStreams.OrderBy(s => s.VideoQuality.MaxHeight).FirstOrDefault();
                case "lowest":
                    return videoStreams.OrderBy(s => s.Size.Bytes).FirstOrDefault();
                case "best":
                default:
                    return videoStreams.OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
            }
        }

        private AudioOnlyStreamInfo GetBestAudioStream(StreamManifest manifest, string quality)
        {
            var audioStreams = manifest.GetAudioOnlyStreams().ToList();

            switch (quality)
            {
                case "lowest":
                    return audioStreams.OrderBy(s => s.Bitrate.BitsPerSecond).FirstOrDefault();
                case "best":
                default:
                    return audioStreams.OrderByDescending(s => s.Bitrate.BitsPerSecond).FirstOrDefault();
            }
        }

        /// <summary>
        /// Validate if URL is a valid YouTube URL
        /// </summary>
        public static bool IsValidYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.Contains("youtube.com/watch") ||
                   url.Contains("youtu.be/") ||
                   url.Contains("youtube.com/shorts/");
        }

        /// <summary>
        /// Extract video ID from YouTube URL
        /// </summary>
        public static string ExtractVideoId(string url)
        {
            try
            {
                var uri = new Uri(url);

                if (uri.Host.Contains("youtu.be"))
                {
                    return uri.AbsolutePath.TrimStart('/');
                }

                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"];
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Result of a download operation
    /// </summary>
    public class DownloadResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string VideoTitle { get; set; }
        public double Duration { get; set; }
        public bool IsCancelled { get; set; }
        public Exception Exception { get; set; }
    }
}