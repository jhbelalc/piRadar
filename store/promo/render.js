// node render.js <lang es|en> <out.mp4> [--stills t1,t2,...]
const puppeteer = require('puppeteer-core');
const ffmpeg = require('ffmpeg-static');
const { spawn } = require('child_process');
const path = require('path');

(async () => {
  const args = process.argv.slice(2); const H = args.includes('--h'); const V = args.includes('--voice'); const [lang = 'es', out = 'out.mp4', flag, list] = args.filter(a => a !== '--h' && a !== '--voice');
  const browser = await puppeteer.launch({
    executablePath: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    headless: true, args: ['--force-color-profile=srgb', '--hide-scrollbars'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: H ? 1920 : 1080, height: H ? 1080 : 1920, deviceScaleFactor: 1 });
  await page.goto('file://' + path.join(__dirname, 'promo-1.5.html') + '?lang=' + lang + (H ? '&fmt=h' : '') + (V ? '&voice=1' : ''));
  await page.evaluate(() => document.fonts.ready);

  if (flag === '--stills') {
    for (const t of list.split(',').map(Number)) {
      await page.evaluate(t => seek(t), t);
      await page.screenshot({ path: `${out}-${t}.jpg`, type: 'jpeg', quality: 80 });
    }
    await browser.close(); return;
  }

  const fps = 30, dur = await page.evaluate(() => DURATION), n = Math.round(dur * fps);
  const ff = spawn(ffmpeg, ['-y', '-f', 'image2pipe', '-framerate', String(fps), '-c:v', 'mjpeg', '-i', '-',
    '-f', 'lavfi', '-i', 'anullsrc=channel_layout=stereo:sample_rate=48000',
    '-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p', '-profile:v', 'high',
    '-c:a', 'aac', '-b:a', '128k', '-shortest', '-movflags', '+faststart', out], { stdio: ['pipe', 'inherit', 'inherit'] });
  for (let i = 0; i < n; i++) {
    await page.evaluate(t => seek(t), i / fps);
    const buf = await page.screenshot({ type: 'jpeg', quality: 95 });
    if (!ff.stdin.write(buf)) await new Promise(r => ff.stdin.once('drain', r));
    if (i % 90 === 0) process.stderr.write(`frame ${i}/${n}\n`);
  }
  ff.stdin.end();
  await new Promise(r => ff.on('close', r));
  await browser.close();
})();
