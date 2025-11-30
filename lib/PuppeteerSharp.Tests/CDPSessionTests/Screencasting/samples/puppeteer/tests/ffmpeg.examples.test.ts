import { spawn } from "node:child_process";
import { describe, it } from "node:test";

/*

    npx tsx --test tests/ffmpeg.examples.test.ts

*/
describe("ffmpeg", () => {

  it("how to spawn ffmpeg", async () => {
    const process = spawn('ffmpeg');
    
    console.log(`${process.spawnfile} ${process.spawnargs}`);

    process.kill('SIGINT');
  });

  // TEST: Show how to print the full ffmpeg command line
});