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
using System.Text.RegularExpressions;

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
                string res = _streamInfo.Resolution.ToString();

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

                    string filePath = saveFileDialog.FileName;

                    //grabbing FFmpeg Path
                    string ffmpegPath = LocateFfmpegPath();
                    if (string.IsNullOrEmpty(ffmpegPath))
                    {
                        MessageBox.Show("FFmpeg path not selected. Operation canceled.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }


                    
                    string tempDirectory = Path.GetTempPath(); // System temp folder
                    string videoPath = Path.Combine(tempDirectory, "video.mp4");
                    string audioPath = Path.Combine(tempDirectory, "audio.mp4");

                    //Downloading Stream
                    bool success = await DownloadAndCombineAsync(_streamInfo, videoPath, audioPath, ffmpegPath, filePath, progressBar, progressText);
                    if (success)
                    {
                        MessageBox.Show("Download complete!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Download Failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }



                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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

                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
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


        private async Task MuxStreamsWithFileSizeTracking(
            string videoPath,
            string audioPath,
            string ffmpegPath,
            string outputPath,
            ProgressBar progressBar,
            TextBlock progressText,
            long totalInputSize)
        {
            var muxingArguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -preset fast \"{outputPath}.mp4\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = muxingArguments,
                    RedirectStandardError = true, // FFmpeg outputs progress to STDERR
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Regex to parse FFmpeg's progress output
            var regex = new Regex(@"frame=.*time=(\d{2}:\d{2}:\d{2})");
            var outputFileInfo = new FileInfo(outputPath);

            // Track progress
            using var reader = process.StandardError;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                // Check the output file size for progress
                outputFileInfo.Refresh(); // Refresh the file info to get the latest size
                long currentSize = outputFileInfo.Exists ? outputFileInfo.Length : 0;

                double progress = (double)currentSize / totalInputSize * 100;

                // Update the UI
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = progress;
                    progressText.Text = $"Muxing progress: {progress:F2}%";
                });
            }

            await process.WaitForExitAsync();

            // Ensure progress is set to 100% when muxing completes
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = 100;
                progressText.Text = "Muxing complete!";
            });
        }

        public async Task<bool> DownloadAndCombineAsync(
            StreamInfo streamInfo,
            string videoPath,
            string audioPath,
            string ffmpegPath,
            string outputPath,
            ProgressBar progressBar,
            TextBlock progressText)
        {

            try
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

                // Reset progress bar for muxing
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 0;
                    progressText.Text = "Muxing streams...";
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
                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 0;
                    progressText.Text = $"Error: {ex.Message}";
                });

                return false;
            }
        }

    }
}
