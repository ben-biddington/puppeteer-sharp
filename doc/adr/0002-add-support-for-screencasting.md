# 2. Add support for screencasting

Date: 2025-08-09

## Status

Accepted

## Context

Screencast is a feature that is [not yet supported and fails if you try to use it](https://github.com/hardkoded/puppeteer-sharp/issues/2943).

## Decision

### Try to replicate how Node JS libary does it

Based on [this](https://github.com/puppeteer/puppeteer/pull/11084) we want an API like:

```js
import puppeteer from "puppeteer";

// Launch a browser
const browser = await puppeteer.launch();

// Create a new page
const page = await browser.newPage();

// Go to your site.
await page.goto("https://www.example.com");

// Start recording.
const recorder = await page.screencast({ path: "recording.webm" });

// Do something.

// Stop recording.
await recorder.stop();

browser.close();
```

This means handling everything including assembling the final video with ffmpeg or similar, so that `recording.webm` is created.

## Consequences

What becomes easier or more difficult to do and any risks introduced by the change that will need to be mitigated.

## Notes

### Running tests from terminal

```sh
dotnet test ./lib/PuppeteerSharp.Tests --filter "PuppeteerSharp.Tests.CDPSessionTests.Screencasting.ScreencastingTests"
```

### Build is very slow, why?

```sh
$ time dotnet test ./lib/PuppeteerSharp.Tests --filter "PuppeteerSharp.Tests.CDPSessionTests.Screencasting.ScreencastingTests"
Restore succeeded with 1 warning(s) in 1.0s
    C:\Users\BenBiddington\sauce\puppeteer-sharp\lib\PuppeteerSharp.Tests\PuppeteerSharp.Tests.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.7 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-rxmq-m78w-7wmc
  PuppeteerSharp.TestServer succeeded (16.6s) → lib\PuppeteerSharp.TestServer\bin\Debug\net8.0\PuppeteerSharp.TestServer.dll
  PuppeteerSharp net8.0 succeeded (42.2s) → lib\PuppeteerSharp\bin\Debug\net8.0\PuppeteerSharp.dll
  PuppeteerSharp.Nunit succeeded (19.5s) → lib\PuppeteerSharp.Nunit\bin\Debug\net8.0\PuppeteerSharp.Nunit.dll
  PuppeteerSharp.Tests succeeded with 1 warning(s) (50.1s) → lib\PuppeteerSharp.Tests\bin\Debug\net8.0\PuppeteerSharp.Tests.dll
    C:\Users\BenBiddington\sauce\puppeteer-sharp\lib\PuppeteerSharp.Tests\PuppeteerSharp.Tests.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.7 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-rxmq-m78w-7wmc
NUnit Adapter 4.6.0.0: Test execution started
Running selected tests in C:\Users\BenBiddington\sauce\puppeteer-sharp\lib\PuppeteerSharp.Tests\bin\Debug\net8.0\PuppeteerSharp.Tests.dll
   NUnit3TestExecutor discovered 1 of 1 NUnit test cases using Current Discovery mode, Non-Explicit run

real    2m20.875s
user    0m0.031s
sys     0m0.125s
```

### ffmpeg framerate

> ffmpeg can be used to change the frame rate of an existing video, such that the output frame rate is lower or higher than the input frame rate. The output duration of the video will stay the same.

> When the frame rate is changed, ffmpeg will drop or duplicate frames as necessary to achieve the targeted output frame rate. -- [trac.ffmpeg.org](https://trac.ffmpeg.org/wiki/ChangingFrameRate)

From [this example of converting still images to an mp4](https://thelinuxcode.com/ffmpeg_images_to_video_tutorial/):

```sh
ffmpeg -i img1.jpg -i img2.jpg -i img3.jpg -i img4.jpg
       -i img5.jpg -i img6.jpg -i img7.jpg -i img8.jpg
       -i img9.jpg -i img10.jpg
       -c:v libx264 -crf 22 -preset medium -pix_fmt yuv420p
       -r 30 slideshow.mp4
```

> This particular example generates a 720p resolution slideshow at 30 fps using the high quality h264 video codec. The CRF value of 22 provides an optimized balance between visual fidelity and file size.

So they are just using `-r 30`.

We're doing this which looks the same:

```c#
// ...
var ffMpeg = FFMpegArguments
            .FromPipeInput(new RawVideoPipeSource(CreateFrames()) { FrameRate = 30 })
            // ...

```

I guess we are really making a [slideshow](https://trac.ffmpeg.org/wiki/Slideshow).

> By using a separate frame rate for the input and output you can control the duration at which each input is displayed and tell ffmpeg the frame rate you want for the output file. This is useful if your player cannot handle a non-standard frame rate. If the input -framerate is lower than the output -r then ffmpeg will duplicate frames to reach your desired output frame rate. If the input -framerate is higher than the output -r then ffmpeg will drop frames to reach your desired output frame rate.

> In this example each image will have a duration of 5 seconds (the inverse of 1/5 frames per second). The video stream will have a frame rate of 30 fps by duplicating the frames accordingly:

```sh
ffmpeg -framerate 1/5 -i img%03d.png -c:v libx264 -r 30 -pix_fmt yuv420p out.mp4
```

Mmm, that did seem to work.

And it did seem to work in the C# version

```c#
var inputFrameRate = 0.5;
var outputFrameRate = 30;

var ffMpeg = FFMpegArguments
    .FromPipeInput(new RawVideoPipeSource(CreateFrames()) { FrameRate = inputFrameRate })
    .OutputToFile(
        _opts.Path,
        true,
        options => options
            .WithVideoCodec(VideoCodec.Png)
            .WithFramerate(outputFrameRate)

            // ...
```

It did make a massive file though.
