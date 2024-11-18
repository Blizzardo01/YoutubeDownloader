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
                    string tempAudioPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "temp_audio.mp4");
                    string tempVideoPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "temp_video.mp4");
                    var filePath = saveFileDialog.FileName;

                    //grabbing FFmpeg Path
                    string ffmpegPath = LocateFfmpegPath();
                    if (string.IsNullOrEmpty(ffmpegPath))
                        {
                        MessageBox.Show("FFmpeg path not selected. Operation canceled.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    //Downloading Stream
                    bool success = await StreamVideoMp4(_url, tempAudioPath, tempVideoPath, ffmpegPath, filePath, _streamInfo);
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
    } 
}
