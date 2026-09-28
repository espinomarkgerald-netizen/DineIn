"""Read-only semantic-ish YAML block comparison, ignoring Unity's block reordering."""
import subprocess, re, difflib
from pathlib import Path
root=Path(__file__).resolve().parents[2]
def blocks(text):
    return {m.group(1):m.group(0) for m in re.finditer(r'^--- !u!\d+ &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)',text,re.M|re.S)}
paths=subprocess.check_output(['git','diff','--name-only'],cwd=root,text=True).splitlines()
for path in paths:
    if not path.endswith(('.unity','.prefab')):continue
    old=blocks(subprocess.check_output(['git','show','HEAD:'+path],cwd=root,text=True))
    new=blocks((root/path).read_text(encoding='utf-8-sig'))
    changes=[k for k in old.keys()&new.keys() if old[k]!=new[k]]
    print(path, 'added',len(new.keys()-old.keys()),'removed',list(old.keys()-new.keys()),'modified',len(changes))
    for key in changes:
        delta=[s for s in difflib.unified_diff(old[key].splitlines(),new[key].splitlines(),n=0) if s[:1] in '+-' and not s.startswith(('+++','---'))]
        print(' ',key,' | '.join(delta[:16]))
