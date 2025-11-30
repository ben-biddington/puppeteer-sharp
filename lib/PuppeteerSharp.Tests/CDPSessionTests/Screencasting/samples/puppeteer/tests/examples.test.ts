import { describe, it } from "node:test";

    import puppeteer from 'puppeteer';

/*

    https://pptr.dev/api/puppeteer.page.screencast

    npx tsx --test tests/examples.test.ts

*/
describe("Screencasting with Puppeteer", () => {
  it("basic example", async () => {

    const browser = await puppeteer.launch();

    const page = await browser.newPage();

    await page.goto("https://www.rnz.co.nz");

    const recorder = await page.screencast({path: 'recording.mp4', format: 'mp4'});

    await page.goto("https://www.rnz.co.nz/news");

    await recorder.stop();

    browser.close();
  });
});