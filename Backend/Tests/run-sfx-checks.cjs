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
test('all 14 supplied clips imported', files.length === 14);
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

// UI-family asset validation: independent authored variants, valid PCM and bounded output.
for (const family of ['menu', 'computer', 'hud']) {
    // Parse the YAML array without assuming checkout line endings.
    const entries = settings.split('  ' + family + 'Clicks:')[1].trimStart().split('\n').slice(0, 3);
    test(family + ' has three distinct authored variants', new Set(entries).size === 3 && entries.every(x => x.trim().startsWith('- {fileID: 8300000,')));
    const buffers = [1, 2, 3].map(i => fs.readFileSync(path.join(root, 'Assets/_Project/Audio/Supplied', family + '-' + i + '.wav')));
    test(family + ' variants are different', !buffers[0].equals(buffers[1]) && !buffers[1].equals(buffers[2]));
    for (const b of buffers) {
        test(family + ' valid short mono PCM', b.toString('ascii', 0, 4) === 'RIFF' && b.readUInt16LE(22) === 1 && b.readUInt32LE(24) === 44100 && b.length < 44100);
        let peak = 0; for (let i = 44; i < b.length; i += 2) peak = Math.max(peak, Math.abs(b.readInt16LE(i)));
        test(family + ' nonempty without digital clipping', peak > 1000 && peak < 32767);
    }
}
test('disabled attempts use blocked cue', button.includes('!button.isActiveAndEnabled') && button.includes('GameplaySfx.Cue.Blocked'));
test('computer windows and HUD have distinct classification', button.includes('ManagementComputerWindow') && button.includes('ButtonFamily.Hud') && button.includes('RestockScene'));
const pouring = read('Assets/_Project/Restaurant/Items/WinePouringUI.cs');
test('pour stops on disable and flowing ends', pouring.includes('pouringAudio.Stop()') && pouring.includes('SetWaterPour(pouringAudio, visible && waterService)'));
const room = read('Assets/_Project/Restaurant/RestockRoom/RestockRoomController.cs');
test('multiplayer placement feedback follows accepted receipt', room.includes('if (accepted && grid != null) grid.PresentPlacement();'));
console.log('PASS UI families, PCM assets, pouring lifecycle and placement reference checks.');

const hold = read('Assets/_Project/Restaurant/RestockRoom/RestockHoldButton.cs');
test('hold closure/release/exit interrupt handling audio', hold.includes('OnDisable() => CancelHold()') && hold.includes('OnPointerUp') && hold.includes('OnPointerExit') && hold.includes('SetDeliveryHold(handlingAudio, false)'));
test('hold does not emit success cue', !hold.includes('Cue.Success') && !hold.includes('HotbarArrived'));
const hud = read('Assets/_Project/Restaurant/RestockRoom/RestockFlowHUD.cs');
const coordinator = read('Assets/_Project/Restaurant/RestockRoom/RestockFlowCoordinator.cs');
test('hotbar sound follows explicit confirmed collection', coordinator.includes('else hud?.ConfirmPickupFeedback();') && hud.includes('if (!pickupFeedbackPending) return;'));
test('ordinary hotbar reconstruction does not emit audio', !hud.split('public void RebuildHotbar()')[1].split('public void HandleSlotClicked')[0].includes('HotbarArrived'));
const cashier = read('Assets/_Project/Lobby/Roles/CashierRegisterUI.cs');
test('payment presentation uses actual credited total in manual and automated paths', (cashier.match(/PaymentCompleted\(.*amountEarned/g) || []).length === 2 && cashier.indexOf('AddEarnings(amountEarned)') < cashier.indexOf('GameplaySfx.PaymentCompleted'));
const paymentTransport = read('Assets/_Project/Networking/Multiplayer/MultiplayerServiceActions.Feedback.cs');
test('payment metadata remains optional and deduplicated before popup', paymentTransport.includes('int paymentAmount = 0') && paymentTransport.indexOf('heardFeedback.Add') < paymentTransport.indexOf('PresentPayment(message.paymentAmount)'));
test('legacy feedback without amount has no payment popup', paymentTransport.includes('message.paymentAmount > 0'));
const popup = read('Assets/_Project/UI/TipPopupUI.cs');
test('payment popup formats currency and honors reduced motion', popup.includes('amount:N0') && popup.includes('!LevelOneUIAccessibility.ReducedMotion'));
test('two conversation voices, seated occupancy and no takeout', playback.includes('new Conversation { source = NewSource() }, new Conversation') && playback.includes('member.IsSeated') && playback.includes('seated < 2') && playback.includes('group.IsTakeout'));
test('conversation uses table location and retained voice assignment', playback.includes('voice.position = voice.group.assignedBooth.transform.position') && playback.includes('if (!eligible && voice.gain <= .001f)'));
test('conversation fade cap and interruption cleanup', playback.includes('CapScale(total') && playback.includes('conversationFadeSeconds') && playback.includes('if (suspended) { voice.source.Stop()'));
test('very quiet ambience and editable zoom hysteresis', settings.includes('ambienceLevel: 0.025') && settings.includes('conversationEnterZoom: 0.35') && settings.includes('conversationExitZoom: 0.45'));
console.log('PASS delivery, payment, interaction and conversation reference checks; live playback remains untested.');
