using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;

namespace ISZxYTubeDownloader
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<DownloadItem> downloadQueue;
        private CancellationTokenSource cancellationTokenSource;
        private bool isPaused = false;
        private int completedCount = 0;
        private string currentFormat = "muxed";
        private string currentQuality = "best";
        private YoutubeClient youtube;

        public MainWindow()
        {
            InitializeComponent();
            downloadQueue = new ObservableCollection<DownloadItem>();
            lstQueue.ItemsSource = downloadQueue;

            // Initialize YoutubeClient with custom HttpClient configuration
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                UseProxy = false,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Add headers to mimic a real browser
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:122.0) Gecko/20100101 Firefox/122.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("DNT", "1");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");

            youtube = new YoutubeClient(httpClient);

            // Set default download path
            txtOutputPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "ISZx Downloads");

            // Initialize default format and quality
            currentFormat = "muxed";
            currentQuality = "best";
        }

        private void CmbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbFormat?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                currentFormat = item.Tag.ToString();

                // Adjust quality options based on format
                if (cmbQuality != null)
                {
                    cmbQuality.IsEnabled = true;
                }
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = txtOutputPath.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtOutputPath.Text = dialog.SelectedPath;
            }
        }

        private void BtnAddUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter a YouTube URL.", "Invalid Input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!url.Contains("youtube.com") && !url.Contains("youtu.be"))
            {
                MessageBox.Show("Please enter a valid YouTube URL.", "Invalid URL",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get current format selection
            var formatItem = cmbFormat?.SelectedItem as ComboBoxItem;
            var qualityItem = cmbQuality?.SelectedItem as ComboBoxItem;

            string formatDisplay = formatItem?.Content?.ToString() ?? "Video + Audio (MP4)";
            string qualityDisplay = qualityItem?.Content?.ToString() ?? "Best Quality";

            string selectedFormat = currentFormat;
            string selectedQuality = currentQuality;

            // Update format/quality from current selection if Tag is not null
            if (formatItem?.Tag != null)
                selectedFormat = formatItem.Tag.ToString();
            if (qualityItem?.Tag != null)
                selectedQuality = qualityItem.Tag.ToString();

            var item = new DownloadItem
            {
                Url = url,
                Status = "Queued",
                Progress = 0,
                ProgressText = "Waiting...",
                StatusColor = Brushes.Gray,
                Format = selectedFormat,
                Quality = selectedQuality,
                FormatInfo = $"{formatDisplay} - {qualityDisplay}"
            };

            downloadQueue.Add(item);
            txtUrl.Clear();
            txtUrl.Text = "https://www.youtube.com/watch?v=";
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (downloadQueue.Count == 0)
            {
                MessageBox.Show("Please add at least one URL to the queue.",
                    "Empty Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(txtOutputPath.Text))
            {
                MessageBox.Show("Please select an output location.",
                    "No Output Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(txtOutputPath.Text);

            btnStart.IsEnabled = false;
            btnPause.IsEnabled = true;
            btnCancel.IsEnabled = true;
            btnAddUrl.IsEnabled = false;
            cmbFormat.IsEnabled = false;
            cmbQuality.IsEnabled = false;

            cancellationTokenSource = new CancellationTokenSource();
            completedCount = 0;
            isPaused = false;

            await StartDownloadProcess();
        }

        private async Task StartDownloadProcess()
        {
            int totalItems = downloadQueue.Count;

            foreach (var item in downloadQueue.ToList())
            {
                if (item.Status == "Completed" || item.Status == "Failed")
                    continue;

                // Check for pause
                while (isPaused && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }

                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    item.Status = "Cancelled";
                    item.StatusColor = Brushes.Orange;
                    break;
                }

                // Retry logic for 403 errors
                int maxRetries = 3;
                int retryCount = 0;
                bool downloadSuccessful = false;

                while (retryCount < maxRetries && !downloadSuccessful)
                {
                    try
                    {
                        if (retryCount > 0)
                        {
                            item.Status = $"Retrying ({retryCount}/{maxRetries})";
                            item.StatusColor = Brushes.Orange;
                            await Task.Delay(3000 * retryCount); // Longer delay between retries

                            // Recreate HttpClient with fresh configuration
                            var handler = new HttpClientHandler
                            {
                                UseCookies = true,
                                UseProxy = false,
                                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                            };

                            var httpClient = new HttpClient(handler)
                            {
                                Timeout = TimeSpan.FromMinutes(10)
                            };

                            httpClient.DefaultRequestHeaders.Clear();
                            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:122.0) Gecko/20100101 Firefox/122.0");
                            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

                            youtube = new YoutubeClient(/*httpClient*/);
                        }

                        item.Status = "Downloading";
                        item.StatusColor = Brushes.Blue;

                        // Get video info
                        var video = await youtube.Videos.GetAsync(item.Url);
                        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

                        // Sanitize filename
                        string fileName = string.Join("_", video.Title.Split(
                            Path.GetInvalidFileNameChars()));

                        string filePath;
                        IStreamInfo streamInfo;

                        // Select stream based on format and quality
                        switch (item.Format)
                        {
                            case "audio":
                                // Audio only - MP3
                                var audioStream = GetBestAudioStream(streamManifest, item.Quality);
                                if (audioStream == null)
                                {
                                    item.Status = "Failed";
                                    item.StatusColor = Brushes.Red;
                                    item.ProgressText = "No audio stream found";
                                    continue;
                                }
                                streamInfo = audioStream;
                                filePath = Path.Combine(txtOutputPath.Text, $"{fileName}.mp3");
                                break;

                            case "video":
                                // Video only (no audio)
                                var videoStream = GetVideoStream(streamManifest, item.Quality);
                                if (videoStream == null)
                                {
                                    item.Status = "Failed";
                                    item.StatusColor = Brushes.Red;
                                    item.ProgressText = "No video stream found";
                                    continue;
                                }
                                streamInfo = videoStream;
                                filePath = Path.Combine(txtOutputPath.Text,
                                    $"{fileName}_video.{videoStream.Container}");
                                break;

                            case "muxed":
                            default:
                                // Video + Audio (muxed)
                                var muxedStream = GetMuxedStream(streamManifest, item.Quality);
                                if (muxedStream == null)
                                {
                                    item.Status = "Failed";
                                    item.StatusColor = Brushes.Red;
                                    item.ProgressText = "No suitable stream found";
                                    continue;
                                }
                                streamInfo = muxedStream;
                                filePath = Path.Combine(txtOutputPath.Text,
                                    $"{fileName}.{muxedStream.Container}");
                                break;
                        }

                        // Download with progress
                        var progress = new Progress<double>(p =>
                        {
                            item.Progress = p * 100;
                            UpdateItemProgress(item, streamInfo.Size.Bytes, p);
                        });

                        // Handle audio conversion for MP3
                        if (item.Format == "audio")
                        {
                            await DownloadAudioAsync(youtube, streamInfo, filePath, progress,
                                cancellationTokenSource.Token);
                        }
                        else
                        {
                            await youtube.Videos.Streams.DownloadAsync(
                                streamInfo,
                                filePath,
                                progress,
                                cancellationTokenSource.Token);
                        }

                        item.Status = "Completed";
                        item.StatusColor = Brushes.Green;
                        item.Progress = 100;
                        item.ProgressText = $"Downloaded: {FormatBytes(streamInfo.Size.Bytes)}";
                        completedCount++;
                        downloadSuccessful = true;
                    }
                    catch (OperationCanceledException)
                    {
                        item.Status = "Cancelled";
                        item.StatusColor = Brushes.Orange;
                        break;
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                    {
                        retryCount++;
                        if (retryCount >= maxRetries)
                        {
                            item.Status = "Failed";
                            item.StatusColor = Brushes.Red;
                            item.ProgressText = "Error: Access forbidden (403). YouTube may be blocking requests.";

                            MessageBox.Show(
                                $"Failed to download: {item.Url}\n\n" +
                                "Error: YouTube returned 403 Forbidden.\n\n" +
                                "Possible solutions:\n" +
                                "1. Try again in a few minutes\n" +
                                "2. Check if the video is available in your region\n" +
                                "3. Verify the video is not age-restricted or private\n" +
                                "4. YouTube may be rate-limiting requests",
                                "Download Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount >= maxRetries)
                        {
                            item.Status = "Failed";
                            item.StatusColor = Brushes.Red;
                            item.ProgressText = $"Error: {ex.Message}";
                        }
                    }
                }

                UpdateOverallProgress(totalItems);
            }

            btnStart.IsEnabled = true;
            btnPause.IsEnabled = false;
            btnCancel.IsEnabled = false;
            btnAddUrl.IsEnabled = true;
            cmbFormat.IsEnabled = true;
            cmbQuality.IsEnabled = true;

            if (completedCount == totalItems)
            {
                MessageBox.Show($"All {completedCount} video(s) downloaded successfully!",
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (completedCount > 0)
            {
                MessageBox.Show($"{completedCount} of {totalItems} video(s) downloaded successfully.",
                    "Download Completed with Errors", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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

        private async Task DownloadAudioAsync(YoutubeClient youtube, IStreamInfo streamInfo,
            string filePath, IProgress<double> progress, CancellationToken cancellationToken)
        {
            // For audio, download to temp location first
            string tempFile = Path.ChangeExtension(filePath, streamInfo.Container.Name);

            await youtube.Videos.Streams.DownloadAsync(
                streamInfo,
                tempFile,
                progress,
                cancellationToken);

            // If MP3 requested and file is not MP3, we keep the original format
            // (Full conversion would require FFmpeg which adds complexity)
            if (Path.GetExtension(filePath) == ".mp3" && Path.GetExtension(tempFile) != ".mp3")
            {
                // Rename to the audio format we actually got
                string actualPath = Path.ChangeExtension(filePath, streamInfo.Container.Name);
                if (File.Exists(tempFile))
                {
                    if (File.Exists(actualPath))
                        File.Delete(actualPath);
                    File.Move(tempFile, actualPath);
                }
            }
            else if (tempFile != filePath)
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tempFile, filePath);
            }
        }

        private void UpdateItemProgress(DownloadItem item, long totalBytes, double progress)
        {
            long downloadedBytes = (long)(totalBytes * progress);
            item.ProgressText = $"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}";
        }

        private void UpdateOverallProgress(int total)
        {
            txtOverallProgress.Text = $"{completedCount} of {total} completed";
            progressOverall.Value = (double)completedCount / total * 100;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            isPaused = !isPaused;
            btnPause.Content = isPaused ? "Resume" : "Pause";
            btnPause.Background = isPaused ?
                new SolidColorBrush(Color.FromRgb(76, 175, 80)) :
                new SolidColorBrush(Color.FromRgb(255, 152, 0));
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to cancel all downloads?",
                "Cancel Downloads",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                cancellationTokenSource?.Cancel();
                btnPause.Content = "Pause";
                isPaused = false;
            }
        }

        private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (btnStart.IsEnabled)
            {
                downloadQueue.Clear();
                completedCount = 0;
                UpdateOverallProgress(0);
            }
            else
            {
                MessageBox.Show("Cannot clear queue while downloading.",
                    "Download in Progress",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string url;
        private string status;
        private double progress;
        private string progressText;
        private Brush statusColor;
        private string format;
        private string quality;
        private string formatInfo;

        public string Url
        {
            get => url;
            set { url = value; OnPropertyChanged(nameof(Url)); }
        }

        public string Status
        {
            get => status;
            set { status = value; OnPropertyChanged(nameof(Status)); }
        }

        public double Progress
        {
            get => progress;
            set { progress = value; OnPropertyChanged(nameof(Progress)); }
        }

        public string ProgressText
        {
            get => progressText;
            set { progressText = value; OnPropertyChanged(nameof(ProgressText)); }
        }

        public Brush StatusColor
        {
            get => statusColor;
            set { statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        public string Format
        {
            get => format;
            set { format = value; OnPropertyChanged(nameof(Format)); }
        }

        public string Quality
        {
            get => quality;
            set { quality = value; OnPropertyChanged(nameof(Quality)); }
        }

        public string FormatInfo
        {
            get => formatInfo;
            set { formatInfo = value; OnPropertyChanged(nameof(FormatInfo)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}