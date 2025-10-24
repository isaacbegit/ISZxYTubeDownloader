# YouTube Downloader

A powerful YouTube video and audio downloader with flexible format and quality options.

## Features

### Format Selection

- **Video + Audio (MP4)** - Complete video with sound (muxed stream)
- **Video Only (MP4)** - Just the video without audio
- **Audio Only (MP3)** - Extract audio track only

### Quality Options

- **Best Quality** - Highest available resolution/bitrate
- **1080p (Full HD)** - 1920x1080 resolution
- **720p (HD)** - 1280x720 resolution
- **480p (SD)** - Standard definition
- **360p (Low)** - Lower quality, smaller file
- **Smallest Size** - Minimum file size (lowest quality)

## How It Works

### Video Formats

- Each quality option targets specific resolutions
- If exact resolution isn't available, it falls back to the closest match
- "Smallest Size" selects the stream with minimum file size
- File sizes vary significantly by quality (360p might be 50MB, 1080p could be 500MB+)

### Audio Only

- Selects the best audio stream available
- "Best Quality" gets highest bitrate audio
- "Smallest Size" gets lowest bitrate (still good quality)
- Saves as audio format (typically M4A or WEBM)

## Usage Example

Download the same video in multiple formats:

1. Add URL with "Video + Audio (MP4)" at "1080p"
2. Add same URL with "Audio Only (MP3)" at "Best Quality"
3. Add same URL with "Video + Audio (MP4)" at "360p" for mobile

Each queue item displays its format:
- Queue shows: "Video + Audio (MP4) - 720p (HD)"
- Progress shows actual file size being downloaded

### Smart Fallback

- If 1080p isn't available, automatically gets best available quality
- Never fails if exact quality doesn't exist

## File Naming

- **Video + Audio**: `VideoTitle.mp4`
- **Video Only**: `VideoTitle_video.mp4`
- **Audio Only**: `VideoTitle.m4a` or `.webm` (native audio format)

---

The format/quality selections are saved per download item, so you can queue multiple downloads of the same video with different formats! 🎉
