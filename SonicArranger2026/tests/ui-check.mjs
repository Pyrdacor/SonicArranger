// The tracker in a real browser: the page of "cshiftc publish SonicArranger2026", driven with Playwright (Chromium).
// It enters notes and a command in edit mode, undoes the last change, saves the module (a download) and checks the
// notes in the saved file, then exports the song as WAV (a download, saved next to the module as .wav: the CI compares
// it with the WAV of the native program and of SonicConvert). Without Playwright it says so and ends with 0.
//
//   cshiftc publish SonicArranger2026 && node SonicArranger2026/tests/ui-check.mjs SonicArranger2026/bin/sonicarranger2026.html [saved.sa]

import { createRequire } from "node:module";
import { execSync } from "node:child_process";
import { mkdtempSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

let playwright;
try {
    playwright = await import("playwright");
} catch {
    try {
        const root = execSync("npm root -g", { encoding: "utf8" }).trim();
        playwright = createRequire(join(root, "noop.js"))("playwright");
    } catch {
        console.log("tracker in a browser: skipped (no Playwright)");
        process.exit(0);
    }
}

const page = pathToFileURL(resolve(process.argv[2])).href;
const saved = process.argv[3] ?? join(mkdtempSync(join(tmpdir(), "sa2026-")), "saved.sa");
const failures = [];
const check = (ok, what) => {
    if (!ok) {
        failures.push(what);
        console.log(`FAILED: ${what}`);
    }
};

const browser = await playwright.chromium.launch();
try {
    const context = await browser.newContext({ viewport: { width: 1200, height: 900 }, acceptDownloads: true });
    const p = await context.newPage();
    await p.goto(page);
    await p.waitForSelector("canvas");
    await p.waitForTimeout(1000);
    const c = await p.locator("canvas").boundingBox();
    const key = async (k) => {
        await p.keyboard.press(k);
        await p.waitForTimeout(40);
    };
    // a click on the note column of voice 1 in the pattern view (no number box has the focus then)
    await p.mouse.click(c.x + 50, c.y + c.height - 40);
    await p.waitForTimeout(200);
    await key("Home");
    await key("Space"); // edit mode
    for (const k of ["z", "x", "c"]) await key(k); // C-2, D-2, E-2 (octave 2), rows 0 to 2
    for (let i = 0; i < 4; i++) await key("ArrowRight"); // the command column
    for (const k of ["f", "3"]) await key(k); // row 3: command F, row 4: command 3
    await key("Control+z"); // undoes row 4
    await p.waitForTimeout(200);

    const download = p.waitForEvent("download", { timeout: 5000 });
    await key("Control+s");
    await p.waitForTimeout(400);
    await p.keyboard.type("saved.sa");
    await key("Enter");
    await (await download).saveAs(saved);
    const bytes = readFileSync(saved);
    check(bytes.subarray(0, 4).toString() === "SOAR", "the download is a module (SOAR)");
    const at = bytes.indexOf("NTBL") + 8;
    const note = (i) => [...bytes.subarray(at + i * 4, at + i * 4 + 4)];
    check(note(0)[0] === 25 && note(1)[0] === 27 && note(2)[0] === 29, `the notes C-2 D-2 E-2: ${[0, 1, 2].map((i) => note(i)[0])}`);
    check(note(3)[2] === 0x0f, `the command F in row 3: ${note(3)}`);
    check(note(4).every((b) => b === 0), `row 4 is empty after undo: ${note(4)}`);
    check(note(64).every((b) => b === 0), "voice 2 has a pattern of its own");

    // the song as WAV (Ctrl+E, the name given: saved.sa.wav)
    const wavDownload = p.waitForEvent("download", { timeout: 20000 });
    await key("Control+e");
    await p.waitForTimeout(400);
    await key("Enter");
    const wav = await wavDownload;
    const wavPath = saved + ".wav";
    await wav.saveAs(wavPath);
    const wavBytes = readFileSync(wavPath);
    check(wav.suggestedFilename() === "saved.sa.wav", `the WAV is named ${wav.suggestedFilename()}`);
    check(wavBytes.subarray(0, 4).toString() === "RIFF" && wavBytes.subarray(8, 16).toString() === "WAVEfmt ", "the export is a WAV file");
    check(wavBytes.length > 44 + 44100, `the WAV has ${wavBytes.length - 44} samples`);
} finally {
    await browser.close();
}
console.log(`tracker in a browser: ${failures.length === 0 ? "ok" : `${failures.length} failed`} (saved ${saved})`);
process.exit(failures.length === 0 ? 0 : 1);
