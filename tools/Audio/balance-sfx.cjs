// Offline audio authoring. Originals are backed up before writing; GUIDs stay intact.
// Usage: node Tools/Audio/balance-sfx.cjs <ffmpeg.exe>
const fs = require('fs'), path = require('path'), cp = require('child_process');
const ffmpeg = process.argv[2];
if (!ffmpeg) throw Error('Supply ffmpeg executable');
const target = -23, peak = -3;
const roots = ['Assets/_Project/Audio/Supplied', 'Assets/_Project/Resources/Audio',
  'Assets/_Project/Restaurant/CookingAssets/Feedback', 'Assets/_Project/Gameplay/Rewards/Art', 'Assets/_Project/Audio/sfx'];
const files = roots.flatMap(r => fs.readdirSync(r).filter(n => /\.(wav|mp3|ogg)$/i.test(n)).map(n => r+'/'+n));
const backup = 'Temp/SfxBalanceOriginals', output = 'Temp/SfxBalanceOutput';
fs.mkdirSync(output, {recursive:true});
function run(args) {
  const p = cp.spawnSync(ffmpeg, ['-hide_banner', '-nostdin', ...args], {encoding:'utf8', maxBuffer:16*1024*1024});
  if (p.status !== 0) throw Error(p.stderr);
  return p.stderr;
}
function measure(p) {
  const log = run(['-i',p,'-af',`loudnorm=I=${target}:TP=${peak}:LRA=11:print_format=json`,'-f','null','-']);
  return JSON.parse(log.slice(log.lastIndexOf('{'),log.lastIndexOf('}')+1));
}
const report=[];
for (const file of files) {
  const original=path.join(backup,file);
  fs.mkdirSync(path.dirname(original), {recursive:true});
  if (!fs.existsSync(original)) fs.copyFileSync(file,original);
  const m=measure(original);
  let filter, method='integrated LUFS';
  if (Number.isFinite(Number(m.input_i))) {
    filter=`loudnorm=I=${target}:TP=${peak}:LRA=11:measured_I=${m.input_i}:measured_TP=${m.input_tp}:measured_LRA=${m.input_lra}:measured_thresh=${m.input_thresh}:offset=${m.target_offset}:linear=true`;
  } else {
    // Sub-400ms cues cannot always produce an integrated LUFS measurement.
    const log=run(['-i',original,'-af','volumedetect','-f','null','-']);
    const rms=Number(log.match(/mean_volume: ([-\d.]+)/)[1]);
    const max=Number(log.match(/max_volume: ([-\d.]+)/)[1]);
    filter=`volume=${Math.min(target-rms,peak-max)}dB`;
    method='short-cue RMS fallback';
  }
  const dest=path.join(output,path.basename(file));
  const codec=/\.mp3$/i.test(file)?['-c:a','libmp3lame','-b:a','192k']:['-c:a','pcm_s16le'];
  run(['-y','-i',original,'-af',filter,'-ar','48000',...codec,dest]);
  const after=measure(dest);
  if (Number(after.input_tp)>-1) throw Error('Insufficient encoded peak headroom: '+file);
  fs.copyFileSync(dest,file);
  report.push({file,method,beforeLUFS:m.input_i,afterLUFS:after.input_i,truePeak:after.input_tp});
  console.log(path.basename(file),after.input_i,after.input_tp);
}
fs.writeFileSync('Tools/Audio/sfx-balance-report.json',JSON.stringify({targetLUFS:target,peakCeilingDb:peak,clips:report},null,2)+'\n');
