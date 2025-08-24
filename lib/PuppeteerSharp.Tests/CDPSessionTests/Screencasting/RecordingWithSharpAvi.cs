using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Helpers.Json;
using PuppeteerSharp.Nunit;
using SharpAvi.Output;

namespace PuppeteerSharp.Tests.CDPSessionTests.Screencasting;

internal sealed class RecordingWithSharpAviTests : PuppeteerPageBaseTest
{
    /*

        Experiment: based on https://pptr.dev/api/puppeteer.page.screencast

    */
    [Test, PuppeteerTest("Screencast.spec", "Page.startScreencast", "should write video to disk")]
    public async Task ShouldWriteVideoToDisk()
    {
        var tempFile = Path.GetTempFileName();
        using var recorder = new SharpAviVideoRecorder(Page);

        await recorder.StartAsync(new VideoRecordingOptions { Path = tempFile, Size = new Size(1484, 705) });

        Console.WriteLine(tempFile);

        await Page.GoToAsync(TestConstants.EmptyPage);

        await Task.Delay(TimeSpan.FromSeconds(5));

        await recorder.StopAsync();

        Assert.That(new FileInfo(tempFile).Length, Is.GreaterThan(0));
    }
}

/*

    [Experiment] Uses SharpApi to record video instead of ffmpeg.

*/
sealed class SharpAviVideoRecorder(IPage page) : IDisposable
{
    private ICDPSession _client;
    private AviWriter _fileWriter;
    private IAviVideoStream _videoStream;
    private VideoRecordingOptions _opts;

    public async Task StartAsync(VideoRecordingOptions opts)
    {
        _opts = opts;

        StartVideo(opts);

        await StartScreencast();
    }

    public async Task StopAsync()
    {
        await StopScreencast();

        StopVideo();
    }

    private async Task StartScreencast()
    {
        _client = await page.CreateCDPSessionAsync();

        _client.MessageReceived += async (_, e) =>
        {
            if (e.MessageID == "Page.screencastFrame")
            {
                await OnScreencastFrameReceived(e);
            }
        };

        await _client.SendAsync("Network.enable");
        await _client.SendAsync("Page.startScreencast");
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
        var sessionId = messageEventArgs.MessageData.GetProperty("sessionId").GetInt32();
        var size = new Size(
            messageEventArgs.MessageData.GetProperty("metadata").GetProperty("deviceWidth").GetInt32(),
            messageEventArgs.MessageData.GetProperty("metadata").GetProperty("deviceHeight").GetInt32());

        var bytes = Convert.FromBase64String(base64EncodedFrame);

        await _videoStream.WriteFrameAsync(true, ToBitmap(bytes, size));

        Console.WriteLine($"Read <{bytes.Length}> bytes");

        // https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-screencastFrameAck
        await _client.SendAsync("Page.screencastFrameAck", new { SessionId = sessionId });
    }

    // If we don't do this we don't get video
    private static byte[] ToBitmap(byte[] screenshot, Size size)
    {
        var frameData = new byte[size.Width * size.Height * 4];

#pragma warning disable CA1416

        using var ms = new MemoryStream(screenshot);
        using var bitmap = new Bitmap(ms);

        // [!] By default, the images backwards and upside down
        bitmap.RotateFlip(RotateFlipType.Rotate180FlipX);

        var bits = bitmap.LockBits(new Rectangle(0, 0, size.Width, size.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);

        Marshal.Copy(bits.Scan0, frameData, 0, frameData.Length);

        bitmap.UnlockBits(bits);

#pragma warning restore CA1416

        return frameData;
    }

    private async Task StopScreencast()
    {
        await _client?.SendAsync("Page.stopScreencast");
        await _client?.DetachAsync();
    }

    private void StartVideo(VideoRecordingOptions opts)
    {
        _fileWriter = new AviWriter(opts.Path)
        {
            EmitIndex1 = true
        };
        _videoStream = _fileWriter.AddVideoStream(opts.Size.Width, opts.Size.Height);
    }

    private void StopVideo()
    {
        _fileWriter?.Close();
        _fileWriter = null;
    }

    public void Dispose()
    {
        _fileWriter?.Close();
    }
}
