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
dotnet test ./lib/PuppeteerSharp.Tests --filter "PuppeteerSharp.Tests.CDPSessionTests.ScreencastingTests"
```
