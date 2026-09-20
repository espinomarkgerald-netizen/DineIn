"""Exercise real CampaignSaveStore file transactions with platform adapters, without starting Unity.
Usage: python tools/verify_fastfood_persistence.py --unity "D:/Unity/Unity/Hub/Editor/6000.0.40f1/Editor"
The adapter covers JSON/public fields and scene/account state; it does not simulate Unity lifecycle or PlayFab.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--unity", type=Path, required=True, help="Installed Unity Editor directory")
args = parser.parse_args()
runtime = args.unity / "Data/NetCoreRuntime"
frameworks = sorted((runtime / "shared/Microsoft.NETCore.App").iterdir(), key=lambda p: tuple(map(int, p.name.split("."))))
framework = frameworks[-1]
output = root / "Temp/FastFoodVerification"
output.mkdir(parents=True, exist_ok=True)
assembly = output / "PersistenceHarness.dll"
refs = [p for p in framework.glob("*.dll") if p.name.startswith(("System.", "Microsoft.CSharp", "Microsoft.VisualBasic", "netstandard", "mscorlib.")) and ".Native." not in p.name]
response = output / "PersistenceHarness.rsp"
response.write_text("\n".join(["-nologo", "-target:exe", "-langversion:latest", f'-out:"{assembly}"'] + [f'-r:"{p}"' for p in refs] + [f'"{root / "Assets/_Project/Save/CampaignSaveStore.cs"}"', f'"{root / "tools/fixtures/fastfood_persistence_harness.cs"}"']), encoding="utf-8")
assembly.with_suffix(".runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net" + ".".join(framework.name.split(".")[:2]), "framework": {"name": "Microsoft.NETCore.App", "version": framework.name}}}), encoding="utf-8")
dotnet = runtime / "dotnet.exe"
subprocess.run([str(dotnet), str(args.unity / "Data/DotNetSdkRoslyn/csc.dll"), "@" + str(response)], cwd=root, check=True)
# Keep the isolated run folder as evidence; never use Application's real persistent-data path.
data = tempfile.mkdtemp(prefix="profile-test-", dir=output)
subprocess.run([str(dotnet), str(assembly), data], cwd=root, check=True)
