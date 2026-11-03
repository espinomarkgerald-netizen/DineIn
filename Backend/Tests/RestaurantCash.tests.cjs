'use strict';
const fs=require('fs'),path=require('path'),cp=require('child_process');
const root=path.resolve(__dirname,'../..'),out=path.resolve(root,'../.codex-checks/RestaurantCashTests');
fs.mkdirSync(out,{recursive:true});
const sdk=cp.execFileSync('dotnet',['--list-sdks'],{encoding:'utf8'}).split(/\r?\n/).map(s=>s.match(/^(9\.\S+) \[(.+)\]/)).filter(Boolean).at(-1);
const pack=path.join(path.dirname(sdk[2]),'packs/Microsoft.NETCore.App.Ref');
const version=fs.readdirSync(pack).filter(v=>v.startsWith('9.')).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true})).at(-1);
const refs=path.join(pack,version,'ref/net9.0'),dll=path.join(out,'RestaurantCashTests.dll'),rsp=path.join(out,'compile.rsp');
fs.writeFileSync(rsp,['-nologo','-nostdlib','-target:exe','-nowarn:0649',`-out:"${dll}"`,...fs.readdirSync(refs).filter(f=>f.endsWith('.dll')).map(f=>`-r:"${path.join(refs,f)}"`),
    `"${path.join(__dirname,'RestaurantCashHarness.cs')}"`,`"${path.join(root,'Assets/_Project/Save/CampaignSaveStore.NormalMoney.cs')}"`].join('\n'));
fs.writeFileSync(path.join(out,'RestaurantCashTests.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm:'net9.0',framework:{name:'Microsoft.NETCore.App',version:'9.0.0'}}}));
cp.execFileSync('dotnet',[path.join(sdk[2],sdk[1],'Roslyn/bincore/csc.dll'),'/noconfig','@'+rsp],{stdio:'inherit',cwd:root});
cp.execFileSync('dotnet',[dll],{stdio:'inherit',cwd:root});
