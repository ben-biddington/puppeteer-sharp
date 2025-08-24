using System.Drawing;

namespace PuppeteerSharp.Tests.CDPSessionTests.Screencasting;

sealed class VideoRecordingOptions
{
    public string Path { get; init; }
    public Size Size { get; init; }
}
