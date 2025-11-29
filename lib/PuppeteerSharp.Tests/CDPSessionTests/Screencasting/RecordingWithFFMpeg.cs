using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PuppeteerSharp.Nunit;
using SharpAvi.Output;

namespace PuppeteerSharp.Tests.CDPSessionTests.Screencasting;

internal sealed class RecordingWithFFMpegTests : PuppeteerPageBaseTest
{
    /*

        Experiment: based on https://pptr.dev/api/puppeteer.page.screencast

        dotnet test ./lib/PuppeteerSharp.Tests --filter "RecordingWithFFMpegTests"

    */
    [Test, PuppeteerTest("Screencast.spec", "Page.startScreencast", "should write video to disk")]
    public async Task ShouldWriteVideoToDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PuppeteerSharpTest");

        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        /*

            Without extension fails with "use a standard extension for the filename or specify the format manually"

        */
        var tempFile = Path.Combine(tempDir, "example-screencast.mp4");

        var recorder = new FFMpegVideoRecorder(Page);

        await recorder.StartAsync(new VideoRecordingOptions { Path = tempFile, Size = new Size(1484, 705) });

        Console.WriteLine(tempFile);

        await Page.GoToAsync("https://www.rnz.co.nz");
        await Page.EvaluateFunctionAsync("() => window.scrollTo(0, 500)");
        await Page.EvaluateFunctionAsync("() => window.scrollTo(0, 1000)");
        await Page.EvaluateFunctionAsync("() => window.scrollTo(0, 2000)");
        await Page.GoToAsync("https://www.rnz.co.nz/news");
        await Page.GoToAsync("https://www.rnz.co.nz/life/people/was-british-nurse-lucy-letby-wrongly-convicted-of-mass-murder-a-kiwi-filmmaker-believes-so");

        await recorder.StopAsync();

        Assert.That(new FileInfo(tempFile).Length, Is.GreaterThan(0));
    }
}
