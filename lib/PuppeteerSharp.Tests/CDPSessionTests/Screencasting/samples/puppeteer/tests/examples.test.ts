import assert from "node:assert";
import { describe, it } from "node:test";

    import puppeteer from 'puppeteer';

/*

    https://pptr.dev/api/puppeteer.page.screencast

*/
describe("Screencasting with Puppeteer", () => {
  it("basic example", async () => {

    const browser = await puppeteer.launch();

    const page = await browser.newPage();

    await page.goto("https://www.rnz.co.nz");

    const recorder = await page.screencast({path: 'recording.webm'});

    await page.goto("https://www.rnz.co.nz/news");

    await recorder.stop();

    browser.close();
  });

  // TEST: Show how to print the full ffmpeg command line
});