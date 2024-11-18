using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;
using Microsoft.Win32;
using System.Net.Http;
using Path = System.IO.Path;
using YoutubeExplode.Videos;

namespace YoutubeDownloader
{
    public partial class Download_Details : Window
    {
        private readonly StreamInfo _streamInfo;
        private readonly string _title;
        private readonly string _url;

        public Download_Details(StreamInfo stream, string title, string url)
        {
            InitializeComponent();
            _streamInfo = stream;
            _title = title;
            _url = url;

            DataContext = stream;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                //Choose a file path for saving
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = _title,
                    DefaultExt = ".mp4",
                    Title = "Save Youtube Video As",
                    Filter = "MP4 Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {

                    var filePath = saveFileDialog.FileName;

                    //grabbing FFmpeg Path
                    string ffmpegPath = LocateFfmpegPath();
                    if (string.IsNullOrEmpty(ffmpegPath))
                    {
                        MessageBox.Show("FFmpeg path not selected. Operation canceled.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    //Downloading Stream
                    /*bool success = await StreamVideoMp4(_url, tempAudioPath, tempVideoPath, ffmpegPath, filePath, _streamInfo);
                    if (success)
                    {
                        MessageBox.Show("Download complete!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Download Failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    } */
                    string tempDirectory = Path.GetTempPath(); // System temp folder
                    string videoPath = Path.Combine(tempDirectory, "video.mp4");
                    string audioPath = Path.Combine(tempDirectory, "audio.mp4");

                    await DownloadAndCombineAsync(_streamInfo, videoPath, audioPath ,ffmpegPath, filePath, progressBar, progressText);


                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public static async Task<bool> StreamVideoMp4(string url, string audioPath, string videoPath, string ffmpegPath, string outputPath, StreamInfo stream)
        {



            try
            {
                var youtube = new YoutubeClient();
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

                // Download video

                var streamInfoAudio = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                var streamInfoVideo = streamManifest.GetVideoOnlyStreams().Where(s => s.Bitrate == stream.Bitrate);


                var videoDownloadTask = youtube.Videos.Streams.DownloadAsync(streamInfoVideo.First(), videoPath).AsTask();
                var audioDownloadTask = youtube.Videos.Streams.DownloadAsync(streamInfoAudio, audioPath).AsTask();

                await Task.WhenAll(videoDownloadTask, audioDownloadTask);

                // Mux streams using FFmpeg
                if (string.IsNullOrEmpty(ffmpegPath) || !IsFfmpegValid(ffmpegPath))
                {
                    MessageBox.Show("Invalid FFmpeg path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var ffmpegArguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -preset fast \"{outputPath}.mp4\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = ffmpegArguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                // Read output/error asynchronously
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();
                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    MessageBox.Show($"FFmpeg failed: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false; // Signal failure
                }

                // Clean up temporary files
                if (File.Exists(videoPath)) File.Delete(videoPath);
                if (File.Exists(audioPath)) File.Delete(audioPath);

                MessageBox.Show("Download and muxing complete.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                return true; // Signal success
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false; // Signal failure
            }
        }


        public static bool IsFfmpegInstalled(out string ffmpegPath)
        {
            ffmpegPath = "ffmpeg"; // Default command

            try
            {
                // Start a process to check if FFmpeg is accessible
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version", // FFmpeg outputs version info if installed
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                // FFmpeg exits with 0 on success
                return process.ExitCode == 0;
            }
            catch
            {
                // If an exception is thrown, FFmpeg is not installed or not in PATH
                return false;
            }
        }

        private string LocateFfmpegPath()
        {
            // Check if FFmpeg is already in PATH
            if (IsFfmpegInstalled(out string pathInPath))
            {
                MessageBox.Show($"FFmpeg found in PATH: {pathInPath}", "FFmpeg Detected");
                return pathInPath;

            }


            //ask the user to locate FFmpeg manually
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Locate FFmpeg Executable"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;


                // Validate the selected file is FFmpeg
                if (IsFfmpegValid(selectedPath))
                {
                    MessageBox.Show($"FFmpeg manually selected: {selectedPath}", "FFmpeg Detected");
                    return selectedPath; //returns the manually selected path
                }

                MessageBox.Show("The selected file is not a valid FFmpeg executable.", "Invalid FFmpeg", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null; // No valid FFmpeg path provided
        }


        private static bool IsFfmpegValid(string ffmpegPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    MessageBox.Show($"FFmpeg validation passed:\n{output}", "FFmpeg Valid");
                    return true;
                }

                MessageBox.Show($"FFmpeg validation failed:\n{error}", "FFmpeg Invalid");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during FFmpeg validation:\n{ex.Message}", "FFmpeg Validation Error");
                return false;
            }
        }


        public async Task DownloadStream(IStreamInfo streamInfo, string downloadPath, IProgress<double> progressReporter, TextBlock progressText)
        {
            var youtube = new YoutubeClient();

            // Download with progress reporting
            await youtube.Videos.Streams.DownloadAsync(streamInfo, downloadPath, progressReporter);

        }


        public async Task MuxStreamsWithFileSizeTracking(
        string videoPath,
        string audioPath,
        string ffmpegPath,
        string outputPath,
        ProgressBar progressBar,
        TextBlock progressText)
        {
            string outputFilePath = $"{outputPath}.mp4";
            var ffmpegArguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -preset fast \"{outputFilePath}\"";

            // Calculate total input size for tracking
            var totalInputSize = new FileInfo(videoPath).Length + new FileInfo(audioPath).Length;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = ffmpegArguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Track file size progress while FFmpeg is running
            var trackingTask = TrackMuxingProgress(outputFilePath, totalInputSize, progressBar, progressText);

            // Wait for FFmpeg to complete
            await Task.WhenAll(process.WaitForExitAsync(), trackingTask);

            if (!process.HasExited)
            {
                process.Kill();
                throw new Exception("FFmpeg did not complete successfully.");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception("FFmpeg encountered an error during muxing.");
            }
        }

        

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }

        private async Task RetryDeleteFileAsync(string filePath, int maxRetries = 5, int delayMilliseconds = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                if (!IsFileLocked(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        return;
                    }
                    catch (IOException ex)
                    {
                        // Handle specific delete exceptions if needed
                        if (i == maxRetries - 1) throw new Exception($"Failed to delete file: {filePath}", ex);
                    }
                }

                // Wait before retrying
                await Task.Delay(delayMilliseconds);
            }
        }

        public async Task DownloadAndCombineAsync(
            StreamInfo streamInfo,
            string videoPath,
            string audioPath,
            string ffmpegPath,
            string outputPath,
            ProgressBar progressBar,
            TextBlock progressText)
            {
                // Progress for downloading video and audio streams
                var videoDownloadProgress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = p / 2; // Half of the progress bar for video
                        progressText.Text = $"Downloading video: {p:F2}%";
                    });
                });

                var audioDownloadProgress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = 50 + p / 2; // Second half for audio
                        progressText.Text = $"Downloading audio: {p:F2}%";
                    });
                });

                // Download tasks
                var youtubeClient = new YoutubeClient();
                var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(_url);


                var streamInfoAudio = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                var streamInfoVideo = streamManifest.GetVideoOnlyStreams().Where(s => s.Bitrate == streamInfo.Bitrate);

                var videoDownloadTask = youtubeClient.Videos.Streams.DownloadAsync(streamInfoVideo.First(), videoPath, videoDownloadProgress).AsTask();
                var audioDownloadTask = youtubeClient.Videos.Streams.DownloadAsync(streamInfoAudio, audioPath, audioDownloadProgress).AsTask();

                await Task.WhenAll(videoDownloadTask, audioDownloadTask);

                // Set progress bar to 100% for downloads
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 100;
                    progressText.Text = "Download complete. Muxing streams...";
                });

                // Muxing progress
                var totalInputSize = new FileInfo(videoPath).Length + new FileInfo(audioPath).Length;

                await MuxStreamsWithFileSizeTracking(
                    videoPath,
                    audioPath,
                    ffmpegPath,
                    outputPath,
                    progressBar,
                    progressText,
                    totalInputSize);

                // Clean up temporary files
                File.Delete(videoPath);
                File.Delete(audioPath);
            }



        private async Task TrackMuxingProgress(
            string outputFilePath,
            long totalInputSize,
            ProgressBar progressBar,
            TextBlock progressText)
            {
                const int updateInterval = 500; // Check progress every 500ms

                while (true)
                {
                    // Break if the file is complete
                    if (File.Exists(outputFilePath) && new FileInfo(outputFilePath).Length >= totalInputSize)
                        break;

                    if (File.Exists(outputFilePath))
                    {
                        // Calculate progress based on file size
                        var outputSize = new FileInfo(outputFilePath).Length;
                        var progress = (double)outputSize / totalInputSize * 100;

                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = progress;
                            progressText.Text = $"Muxing: {progress:F2}% completed";
                        });
                    }

                    await Task.Delay(updateInterval);
                }

                // Final update
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 100;
                    progressText.Text = "Muxing Complete!";
                });
        }

        public async Task MuxStreamsWithFileSizeTracking(
            string videoPath,
            string audioPath,
            string ffmpegPath,
            string outputPath,
            ProgressBar progressBar,
            TextBlock progressText,
            long totalInputSize)
        {
            string outputFilePath = $"{outputPath}.mp4";
            var ffmpegArguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -preset fast \"{outputFilePath}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = ffmpegArguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Track file size progress while FFmpeg is running
            var trackingTask = TrackMuxingProgress(outputFilePath, totalInputSize, progressBar, progressText);

            // Wait for FFmpeg to complete
            await Task.WhenAll(process.WaitForExitAsync(), trackingTask);

            if (!process.HasExited)
            {
                process.Kill();
                throw new Exception("FFmpeg did not complete successfully.");
            }

            if (process.ExitCode != 0)
            {
                throw new Exception("FFmpeg encountered an error during muxing.");
            }
        }
    }
}
