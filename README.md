# Youtube Downloader

## Description
YouTube Downloader is an easy-to-use GUI application that allows you to download YouTube videos or extract audio seamlessly. 
Perfect for enjoying your favorite content for offline use.

### Key Features:
-Download videos in various stream qualities
-Download audio in various bitrates

## Installation Requirements

### For End Users
1. [[.NET Runtime](https://dotnet.microsoft.com/download) version 6.0 or higher]
2. [[FFmpeg](https://ffmpeg.org/)]

### For Developers
1. [.NET Runtime](https://dotnet.microsoft.com/download) version 6.0 or higher.
2. Install required NuGet packages:
  - [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) (for video/audio download).
  - [Newtonsoft.Json](https://www.newtonsoft.com/json) (for JSON-based settings persistence).
  - [HtmlAgilityPack](https://html-agility-pack.net/download) (for HTML parsing).
3. Restore NuGet dependencies using the command:
   ```bash
   dotnet restore
   ```

## Configuration

You can customize the ffmpeg file path by editing the `config.json` file located in the application folder:

```json
{
  "FFmpeg": "C:\\Users\\YourName\\FFmpegPathHere"
}
```

#### Contributing

If you wish to add, change or modify this project, go for it!,
just follow these steps:

```markdown
1. Fork this repository.
2. Create a new branch (`git checkout -b feature/YourFeature`).
3. Commit your changes (`git commit -m 'Add YourFeature'`).
4. Push the branch (`git push origin feature/YourFeature`).
5. Open a Pull Request.
```
## Acknowledgments
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) for YouTube interaction.
- [Newtonsoft.Json](https://www.newtonsoft.com/json) for JSON handling.
- [HtmlAgilityPack](https://html-agility-pack.net/download) for HTML parsing.
- FFmpeg for media processing.


