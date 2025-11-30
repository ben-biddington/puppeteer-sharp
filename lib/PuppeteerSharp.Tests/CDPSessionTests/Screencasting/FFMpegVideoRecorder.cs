using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using FFMpegCore.Extensions.System.Drawing.Common;
using FFMpegCore.Pipes;
using PuppeteerSharp.Helpers.Json;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace PuppeteerSharp.Tests.CDPSessionTests.Screencasting;

/*

    https://github.com/puppeteer/puppeteer/blob/main/packages/puppeteer-core/src/node/ScreenRecorder.ts

*/

// ReSharper disable once InconsistentNaming
sealed class FFMpegVideoRecorder(IPage page)
{
    private ICDPSession _client;
    private VideoRecordingOptions _opts;
    private readonly ConcurrentQueue<IVideoFrame> _frames = new();
    private bool _done = false;
    private Task _endTask;

    public async Task StartAsync(VideoRecordingOptions opts)
    {
        _opts = opts;

        await StartScreencast();

        _endTask = Task.Run(StartVideoAsync);
    }

    public async Task StopAsync()
    {
        await StopScreencast();

        await StopVideo();
    }

    /*

        StreamPipeSource fails with

            [in#0 @ 0000021abe4ef340] Error opening input: Invalid argument
            Error opening input file \\.\pipe\FFMpegCore_b9e00.
            Error opening input files: Invalid argument)

        I suspect the stream is meant to be a single video file by the looks of the tests (https://github.com/rosenbjerg/FFMpegCore/blob/main/FFMpegCore.Test/VideoTest.cs).

        So we'll have to try again with RawVideoPipeSource (https://github.com/rosenbjerg/FFMpegCore?tab=readme-ov-file#input-piping).

        Our issue is that the frames arrive asynchronously from and event handler.
    */
    // https://github.com/rosenbjerg/FFMpegCore?tab=readme-ov-file#input-piping
    private async Task StartScreencast()
    {
        _client = await page.CreateCDPSessionAsync();

        _client.MessageReceived += async (_, e) =>
        {
            if (e.MessageID == "Page.screencastFrame")
            {
                Console.WriteLine($"[{DateTime.Now:O}] Page.screencastFrame received");
                await OnScreencastFrameReceived(e);
            }
        };

        await _client.SendAsync("Network.enable");
        await _client.SendAsync("Page.startScreencast");
    }

    private async Task StartVideoAsync()
    {
        IEnumerable<IVideoFrame> CreateFrames()
        {
            while (!_done)
            {
                IVideoFrame next = null;

                Console.WriteLine(
                    $"[{Thread.CurrentThread.ManagedThreadId}] CreateFrames <{_frames.Count}> [done={_done}]");

                var now = DateTime.Now;

                bool TimedOut() => DateTime.Now.Subtract(now) > TimeSpan.FromSeconds(20);

                while (!_done && _frames.TryDequeue(out next) == false)
                {
                    if (TimedOut())
                    {
                        throw new Exception($"[{Thread.CurrentThread.ManagedThreadId}] Timed out waiting for frame");
                    }

                    Console.WriteLine($"[{DateTime.Now}][{Thread.CurrentThread.ManagedThreadId}] Waiting for frame...");
                    Thread.Sleep(500);
                }

                if (_done)
                    yield break;

                Console.WriteLine($"[done={_done}] Returning frame, there are <{_frames.Count}> left after this");

                yield return next;
            }
        }

        Console.WriteLine($"[{DateTime.Now}] Starting FFMPEG");

        var inputFrameRate = 0.5;
        var outputFrameRate = 10;

        var ffMpeg = FFMpegArguments
            .FromPipeInput(new RawVideoPipeSource(CreateFrames()) { FrameRate = inputFrameRate /* Show each frame for 5s <https://trac.ffmpeg.org/wiki/Slideshow> */})
            .OutputToFile(
                _opts.Path,
                true,
                options => options
                    .WithVideoCodec(VideoCodec.Png) /* VideoCodec.Png is important */
                    /*
                        Trying to force ffmpeg to fill in frames because we don't get that many.

                        The idea is that it just fills frames with the last one until a new one arrives.

                        https://www.reddit.com/r/ffmpeg/comments/nf960l/fill_missing_video_frames/

                        * FpsArgument did not work

                    */
                    .WithFramerate(outputFrameRate)
                    /*

                        Copying from https://github.com/puppeteer/puppeteer/blob/main/packages/puppeteer-core/src/node/ScreenRecorder.ts

                    */
                    .WithArgument(new CustomArgument("-avioflags direct"))
                    .WithArgument(new CustomArgument("-fpsprobesize 0"))
                    .WithArgument(new CustomArgument("-probesize 32"))
                    .WithArgument(new CustomArgument("-analyzeduration 0"))
                    .WithArgument(new CustomArgument("-fflags nobuffer"))
            /*
                Disable bitrate like?

                https://github.com/puppeteer/puppeteer/pull/11084/files#diff-a0ae5e9e96944abb96ac9f3754a1c07af35171a8276701273b76114872461850R122
            */
            //.WithVideoBitrate(0)
            // .WithVideoFilters(videoFilterOptions =>
            // {
            //     videoFilterOptions.Arguments.Add(
            //         /* https://usercomp.com/news/1058757/ffmpeg-convert-vfr-to-cfr-without-timing-issues */
            //         new SetPtsArgument("PTS/1"));
            // })
            )
            .WithLogLevel(FFMpegLogLevel.Debug)
            .NotifyOnProgress(percentage => { Console.WriteLine($"Progress <{percentage}>"); });

        Console.WriteLine($"[{DateTime.Now}] ${ffMpeg.Arguments}");

        await ffMpeg.ProcessAsynchronously();

        Console.WriteLine($"[{DateTime.Now}] FFMPEG finished");
    }

    private async Task OnScreencastFrameReceived(MessageEventArgs messageEventArgs)
    {
        /*
        {
            "messageID": "Page.screencastFrame",
            "messageData": {
                "data": "iVB...",
                "metadata": {
                    "offsetTop": 0,
                    "pageScaleFactor": 1,
                    "deviceWidth": 800,
                    "deviceHeight": 600,
                    "scrollOffsetX": 0,
                    "scrollOffsetY": 0,
                    "timestamp": 1755656804.325671
                },
                "sessionId": 1
            }
        }*/

        var base64EncodedFrame = messageEventArgs.MessageData.GetProperty("data").GetString();
        /*

            File is png:

                PNG
                
                ���
                IHDR�� ��X���vp�� �IDATx
        */
        var sessionId = messageEventArgs.MessageData.GetProperty("sessionId").GetInt32();

        var size = new Size(
            messageEventArgs.MessageData.GetProperty("metadata").GetProperty("deviceWidth").GetInt32(),
            messageEventArgs.MessageData.GetProperty("metadata").GetProperty("deviceHeight").GetInt32());

        var bytes = Convert.FromBase64String(base64EncodedFrame);

        // https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-screencastFrameAck
        await _client.SendAsync("Page.screencastFrameAck", new { SessionId = sessionId });

        EnqueueFrame(bytes, size);
    }

    private void EnqueueFrame(byte[] bytes, Size size)
    {
#pragma warning disable CA1416
        using var ms = new MemoryStream(bytes);
        using var png = new Bitmap(ms);

#pragma warning disable CA2000

        var bitmap = ToBitmap(png, size);

        // Yes, this is working, I can see the files and they look correct.
        Save(bitmap);

        var frame = new BitmapVideoFrameWrapper(bitmap);

        _frames.Enqueue(frame);
#pragma warning restore CA2000
#pragma warning restore CA1416
        Console.WriteLine($"[{DateTime.Now:O}, {Thread.CurrentThread.ManagedThreadId}] Enqueued frame with size <{size.Width}x{size.Height}>, there are now <{_frames.Count}> frames");
    }

    private void Save(Bitmap bitmap)
    {
        var dir = Path.GetDirectoryName(_opts.Path);
#pragma warning disable CA1416
        var filename = Path.Combine(dir, $"{FrameFilename()}.png");
        bitmap.Save(filename);
        Console.WriteLine($"[{DateTime.Now:O}] Saved frame to {filename}");
#pragma warning restore CA1416
    }

    private static Guid FrameFilename() => Guid.NewGuid();

#pragma warning disable CA1416
    private Bitmap ToBitmap(Bitmap pngImage, Size size)
    {
        var bitmapImage = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmapImage);

        graphics.DrawImage(pngImage, new Rectangle(0, 0, size.Width, size.Height));

        return bitmapImage;
    }
#pragma warning restore CA1416

    private async Task StopScreencast()
    {
        await _client?.SendAsync("Page.stopScreencast");
        await _client?.DetachAsync();
    }

    private async Task StopVideo()
    {
        SpinWait.SpinUntil(() => _frames.IsEmpty, TimeSpan.FromSeconds(30));

        _done = true;

        await _endTask;
    }
}

internal sealed class SetPtsArgument(string value) : IVideoFilterArgument
{
    public string Key => "setpts";
    public string Value { get; } = value;
}

internal sealed class FpsArgument(string value) : IVideoFilterArgument
{
    public string Key { get; } = "fps";
    public string Value { get; } = value;
}
