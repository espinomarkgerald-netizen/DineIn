// Compile the source-linked harness without NuGet, Unity, network or user NuGet configuration.
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const output = path.resolve(root, '../.codex-checks/RunReportingHarness');
const sdks = cp.execFileSync('dotnet', ['--list-sdks'], { encoding: 'utf8' }).trim().split(/\r?\n/)
    .map(line => line.match(/^(9\.\S+) \[(.+)\]$/)).filter(Boolean);
if (!sdks.length) throw Error('Install a .NET 9 SDK and runtime to run these headless tests.');
const sdk = sdks.sort((a, b) => a[1].localeCompare(b[1], undefined, { numeric: true })).at(-1);
const dotnetRoot = path.dirname(sdk[2]);
const refRoot = path.join(dotnetRoot, 'packs/Microsoft.NETCore.App.Ref');
const refVersion = fs.readdirSync(refRoot).filter(v => v.startsWith('9.'))
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true })).at(-1);
const references = path.join(refRoot, refVersion, 'ref/net9.0');
const runtime = cp.execFileSync('dotnet', ['--list-runtimes'], { encoding: 'utf8' }).split(/\r?\n/)
    .map(line => line.match(/^Microsoft.NETCore.App (9\.\S+) /)).filter(Boolean)
    .sort((a, b) => a[1].localeCompare(b[1], undefined, { numeric: true })).at(-1);
if (!runtime) throw Error('A .NET 9 runtime is required.');
const sources = [...fs.readFileSync(path.join(__dirname, 'RunReportingHarness.csproj'), 'utf8')
    .matchAll(/Compile Include="([^"]+)"/g)].map(m => path.resolve(__dirname, m[1]));
fs.mkdirSync(output, { recursive: true });
const dll = path.join(output, 'RunReportingHarness.dll');
const response = path.join(output, 'compile.rsp');
fs.writeFileSync(response, ['-nologo', '-nostdlib', '-target:exe', '-nowarn:0649', `-out:"${dll}"`,
    ...fs.readdirSync(references).filter(f => f.endsWith('.dll')).map(f => `-r:"${path.join(references, f)}"`),
    ...sources.map(f => `"${f}"`)].join('\n'));
fs.writeFileSync(path.join(output, 'RunReportingHarness.runtimeconfig.json'), JSON.stringify({
    runtimeOptions: { tfm: 'net9.0', framework: { name: 'Microsoft.NETCore.App', version: runtime[1] } }
}));
cp.execFileSync('dotnet', [path.join(sdk[2], sdk[1], 'Roslyn/bincore/csc.dll'), '/noconfig', '@' + response], { stdio: 'inherit', cwd: root });
cp.execFileSync('dotnet', [dll], { stdio: 'inherit', cwd: root });
