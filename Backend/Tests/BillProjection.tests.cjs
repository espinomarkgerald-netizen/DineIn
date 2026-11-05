'use strict';
// Execute the production projection predicates with minimal Unity boundary doubles.
const fs=require('fs'),path=require('path'),cp=require('child_process');
const root=path.resolve(__dirname,'../..'),out=path.resolve(root,'../.codex-checks/BillProjectionTests');
fs.mkdirSync(out,{recursive:true});
const source=fs.readFileSync(path.join(root,'Assets/_Project/Networking/Multiplayer/MultiplayerCustomerInteractionBridge.cs'),'utf8');
const methods=source.slice(source.indexOf('    private static bool IsMatchingHeldBill'),source.indexOf('    private string BillWaitReason'));
const harness=`using System;
class Transform { public Transform parent; public bool IsChildOf(Transform t)=>parent==t; }
class CustomerGroup {}
class WaiterHands { public CustomerGroup holdingBillFor; public Transform BillHoldPoint=new(); }
class BillPaper { public CustomerGroup group; public Transform transform=new(); public WaiterHands holder; public bool Matches(CustomerGroup g)=>g==group; public T GetComponentInParent<T>(bool includeInactive) where T:class=>holder as T; }
class MultiplayerCustomerSpawn { public enum BillStage { None, Printing, Printed, Carried, Complete } }
class Test {
${methods}
static void Main(){ int n=0; var g=new CustomerGroup(); var other=new CustomerGroup(); var h=new WaiterHands{holdingBillFor=g}; var p=new BillPaper{group=g};
foreach(MultiplayerCustomerSpawn.BillStage actual in Enum.GetValues<MultiplayerCustomerSpawn.BillStage>())
foreach(MultiplayerCustomerSpawn.BillStage expected in Enum.GetValues<MultiplayerCustomerSpawn.BillStage>())
foreach(bool held in new[]{false,true}) foreach(bool matching in new[]{false,true}) foreach(bool owner in new[]{false,true}) {
p.group=matching?g:other; p.holder=held?h:null; p.transform.parent=held?h.BillHoldPoint:null; h.holdingBillFor=owner?g:other;
bool want=matching && ((actual==MultiplayerCustomerSpawn.BillStage.Printed && expected==MultiplayerCustomerSpawn.BillStage.Printed && !held) || (actual==MultiplayerCustomerSpawn.BillStage.Carried && held && owner && (expected==MultiplayerCustomerSpawn.BillStage.Printed || expected==MultiplayerCustomerSpawn.BillStage.Carried)));
if(BillProjectionReady(h,g,p,actual,expected)!=want) throw new Exception($"Mismatch {actual} {expected} {held} {matching} {owner}"); n++;
} Console.WriteLine($"PASS {n} production billing projection combinations; Unity boundaries are doubles."); }}
`;
fs.writeFileSync(path.join(out,'Harness.cs'),harness);
const sdk=cp.execFileSync('dotnet',['--list-sdks'],{encoding:'utf8'}).split(/\r?\n/).map(s=>s.match(/^(9\.\S+) \[(.+)\]/)).filter(Boolean).at(-1);
const pack=path.join(path.dirname(sdk[2]),'packs/Microsoft.NETCore.App.Ref'),version=fs.readdirSync(pack).filter(v=>v.startsWith('9.')).at(-1),refs=path.join(pack,version,'ref/net9.0'),dll=path.join(out,'Harness.dll'),rsp=path.join(out,'compile.rsp');
fs.writeFileSync(rsp,['-nologo','-nostdlib','-target:exe',`-out:"${dll}"`,...fs.readdirSync(refs).filter(f=>f.endsWith('.dll')).map(f=>`-r:"${path.join(refs,f)}"`),`"${path.join(out,'Harness.cs')}"`].join('\n'));
fs.writeFileSync(path.join(out,'Harness.runtimeconfig.json'),JSON.stringify({runtimeOptions:{tfm:'net9.0',framework:{name:'Microsoft.NETCore.App',version:'9.0.0'}}}));
cp.execFileSync('dotnet',[path.join(sdk[2],sdk[1],'Roslyn/bincore/csc.dll'),'/noconfig','@'+rsp],{stdio:'inherit'});
cp.execFileSync('dotnet',[dll],{stdio:'inherit'});
