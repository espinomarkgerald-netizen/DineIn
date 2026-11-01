// Focused source/asset checks plus actual pure C# policy tests, without Unity or NuGet.
'use strict';
const fs = require('node:fs'), path = require('node:path'), cp = require('node:child_process');
const assert = require('node:assert/strict'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/\r\n/g, '\n');
let checks = 0;
function test(name, condition) { assert.ok(condition, name); checks++; console.log('PASS ' + name); }
const settings = read('Assets/_Project/Resources/Audio/GameplaySfx.asset');
const files = fs.readdirSync(path.join(root, 'Assets/_Project/Audio/Supplied')).filter(f => f.endsWith('.mp3'));
test('all 12 supplied clips imported', files.length === 12);
for (const name of files) {
    const file = path.join(root, 'Assets/_Project/Audio/Supplied', name), meta = fs.readFileSync(file + '.meta', 'utf8');
    test(name + ' assigned in shared SFX configuration', settings.includes(meta.match(/guid: (\w+)/)[1]));
    const original = path.join('G:/Downloads/Dine In sfx', name);
    if (fs.existsSync(original)) test(name + ' original bytes preserved', fs.readFileSync(original).equals(fs.readFileSync(file)));
    else console.log('NOT RUN original comparison: ' + name);
    if (name.startsWith('20-Minute')) test('ambience streams without preloading', /loadType: 2/.test(meta) && /preloadAudioData: 0/.test(meta) && /loadInBackground: 1/.test(meta));
}
const sfx = read('Assets/_Project/Audio/GameplaySfx.cs');
const playback = read('Assets/_Project/Audio/GameplaySfxPlayback.cs');
const button = read('Assets/_Project/Audio/GameplayButtonSound.cs');
test('original network cue IDs retained', sfx.includes('enum Cue { Serve, Payment, Clean, Success, Error,'));
test('UI is unpositioned', sfx.includes('PlayCue(cue, false, default)'));
test('missing camera suppresses world audio', playback.includes('if (view == null || StorageOpen) return 0;'));
test('distance and zoom curves are authored', settings.includes('distanceFalloff:') && settings.includes('zoomFalloff:'));
test('voice pitches set only after a free voice is selected', playback.indexOf('if (voice.source.isPlaying || voice.paused) continue;') < playback.indexOf('voice.source.pitch = pitches.Next'));
test('voice pool never steals playing voices', playback.includes('return false;') && !playback.includes('PlayOneShot('));
test('UI pointer/submit/onClick share a frame deduplicator', button.includes('IPointerClickHandler, ISubmitHandler') && button.includes('playedFrame == Time.frameCount') && button.includes('onClick.AddListener(Activated)'));
test('short pitch does not consume gameplay randomness', !sfx.includes('UnityEngine.Random') && !read('Assets/_Project/Audio/GameplaySfxPolicy.cs').includes('Random.Range'));
test('ambience crossfade and pause are presentation-only', playback.includes('source.Pause()') && playback.includes('source.UnPause()') && playback.includes('bed[1 - activeBed].volume = bedGain * crossfade'));
test('accepted tutorial actions do not require a running service clock', !playback.split('private static bool Suspended')[1].split('private static bool StorageOpen')[0].includes('Time.timeScale') && !sfx.includes('ServiceActive'));
test('SFX mixer assignment is retained', settings.includes('guid: 3fd0d86c1f880854d9b5971fafc00816'));
const settingsManager = read('Assets/_Project/MainMenu/NewDesign/Scripts/Settings/SettingsManager.cs');
test('existing SFX slider and master mute remain authoritative', settingsManager.includes('SetMixer(gameplayMixer, "SFXVol", Current.sfxVolume)') && settingsManager.includes('AudioListener.volume = Current.muteAll ? 0f : Current.masterVolume'));
const transport = read('Assets/_Project/Networking/Multiplayer/MultiplayerServiceActions.Feedback.cs');
test('optional position plus legacy 2D fallback', transport.includes('public bool hasPosition;') && transport.includes('else GameplaySfx.PresentAction((GameplaySfx.Cue)message.cue);'));
test('authority/uncached/deduplicated live feedback preserved', transport.includes('!session.IsAuthority') && transport.includes('EventCaching.DoNotCache') && transport.includes('!heardFeedback.Add(message.action)') && transport.includes('session.IsRecovering'));
test('invalid world coordinates rejected', transport.includes('!GameplaySfxView.Finite(message.position.x)'));
const horn = read('Assets/_Project/Restaurant/RestockRoom/RestockTruckInteractable.cs');
test('two horn beeps keep timing and use one gain application', (horn.match(/PlayWorldClip\(arrivalHornClip/g) || []).length === 2 && horn.includes('WaitForSecondsRealtime') && !horn.includes('hornSource.volume'));
test('authored truck volume lowered', read('Assets/_Project/Resources/RestockFlow/RestockDeliveryTruck.prefab').includes('hornVolume: 0.15'));
const cooking = read('Assets/_Project/Restaurant/FastFoodCookingView.Feedback.cs');
test('cooking loop pitch changes only at start/loop boundary', cooking.includes('source.timeSamples < previousCookingSample') && cooking.includes('restarting || loopBoundary'));
const game = read('Assets/_Project/Gameplay/GameManager/GameManager.cs');
test('new results use end-day cue behind existing restoration guard', game.includes('GameplaySfx.Play(GameplaySfx.Cue.EndDay)') && game.includes('resultsCuePending = !instantResults && !restoringResultsPresentation'));
const names = ['Lobby1','Lobby2','Lobby3','Lobby1Tutorial','Lobby2Tutorial','Lobby3Tutorial','Lobby1 Multiplayer','Lobby2 Multiplayer','Lobby3 Multiplayer'];
const build = read('ProjectSettings/EditorBuildSettings.asset');
for (const name of names) {
    const scene = name === 'Lobby1Tutorial' ? 'Assets/_Project/Scenes/TutorialScenes/' + name + '.unity' : 'Assets/_Project/Scenes/RoleBased/' + name + '.unity';
    test(name + ' shared playback context and enabled destination', playback.includes('"' + name + '"') && build.includes('- enabled: 1\n    path: ' + scene));
}
console.log(checks + ' SFX source/asset/reference checks passed; runtime/listening checks remain manual.');
const output = path.resolve(root, '../.codex-checks/SfxPolicy'); fs.mkdirSync(output, { recursive: true });
const sdk = cp.execFileSync('dotnet', ['--list-sdks'], { encoding: 'utf8' }).split(/\r?\n/).map(s => s.match(/^(9\.\S+) \[(.+)\]$/)).filter(Boolean).at(-1);
if (!sdk) throw Error('A .NET 9 SDK/runtime is needed for the pure policy tests.');
const dotnet = path.dirname(sdk[2]), packs = path.join(dotnet, 'packs/Microsoft.NETCore.App.Ref');
const version = fs.readdirSync(packs).filter(v => v.startsWith('9.')).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true})).at(-1);
const refs = path.join(packs, version, 'ref/net9.0');
const runtime = cp.execFileSync('dotnet', ['--list-runtimes'], { encoding: 'utf8' }).match(/Microsoft.NETCore.App (9\.\S+) /)[1];
const dll = path.join(output, 'SfxPolicy.dll'), rsp = path.join(output, 'compile.rsp');
fs.writeFileSync(rsp, ['-nologo','-nostdlib','-target:exe',`-out:"${dll}"`,
    ...fs.readdirSync(refs).filter(f=>f.endsWith('.dll')).map(f=>`-r:"${path.join(refs,f)}"`),
    `"${path.join(root,'Assets/_Project/Audio/GameplaySfxPolicy.cs')}"`, `"${path.join(__dirname,'SfxPolicyHarness.cs')}"`].join('\n'));
fs.writeFileSync(path.join(output,'SfxPolicy.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm:'net9.0',framework:{name:'Microsoft.NETCore.App',version:runtime}}}));
cp.execFileSync('dotnet',[path.join(sdk[2],sdk[1],'Roslyn/bincore/csc.dll'),'/noconfig','@'+rsp],{stdio:'inherit'});
cp.execFileSync('dotnet',[dll],{stdio:'inherit'});
