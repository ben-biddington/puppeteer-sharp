# Install

```sh
nvm use `cat .nvmrc`
```

```sh
npm ci
```

```sh
npm test
```

# Debug printing

From [the source code](https://github.com/puppeteer/puppeteer)

Using `puppeteer:ffmpeg` doesn't show much

```sh
$ DEBUG=puppeteer:ffmpeg npm test

> puppeteer@1.0.0 test
> npx tsx --test tests/**/*test.ts

2025-11-30T19:02:28.022Z puppeteer:ffmpeg [png @ 000001527ce8f440] Invalid PNG signature 0xD494844520000.

▶ Screencasting with Puppeteer
  ✔ basic example (5937.0312ms)
✔ Screencasting with Puppeteer (5938.4086ms)
ℹ tests 1
ℹ suites 1
ℹ pass 1
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 8358.4145
```

Manual debug, find the the file:

```sh
grep -Eir ScreenRecorder node_modules/puppeteer-core/
node_modules/puppeteer-core/lib/cjs/puppeteer/api/Page.d.ts:import type { ScreenRecorder } from '../node/ScreenRecorder.js';
```

or:

```sh
$ find node_modules/ -name ScreenRecorder.js -type f
node_modules/puppeteer-core/lib/cjs/puppeteer/node/ScreenRecorder.js
node_modules/puppeteer-core/lib/esm/puppeteer/node/ScreenRecorder.js
```

Mine ended up being at:

```sh
node_modules\puppeteer-core\lib\cjs\puppeteer\node\ScreenRecorder.js
```

Something like this might work:

```ts
// node_modules\puppeteer-core\lib\cjs\puppeteer\node\ScreenRecorder.js
console.log(`${this.#process.spawnfile} ${this.#process.spawnargs}`);
```

```sh
ffmpeg ffmpeg,-loglevel,error,-avioflags,direct,-fpsprobesize,0,-probesize,32,-analyzeduration,0,-fflags,nobuffer,-f,image2pipe,-vcodec,png,-i,pipe:0,-an,-threads,1,-framerate,30,-b:v,0,-vcodec,vp9,-crf,30,-deadline,realtime,-cpu-used,8,-f,webm,-vf,crop='min(800,iw):min(600,ih):0:0',pad=800:600:0:0,-y,pipe:1
```

Compre using `.WithLogLevel(FFMpegLogLevel.Debug)`:

```sh
-f rawvideo -r 0.5 -pix_fmt bgra -s 800x600 -i "\\.\pipe\FFMpegCore_787e5" -c:v png -r 10 "C:\Users\BenBiddington\AppData\Local\Temp\PuppeteerSharpTest\example-screencast.mp4" -y
```
