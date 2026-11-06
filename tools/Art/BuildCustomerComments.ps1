$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$pack=Join-Path $project 'Assets/_Project/UI/Assets/CustomerCommentSpritePackV1'
Add-Type -AssemblyName System.Drawing
$refs=@([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,'System.Runtime','System.ComponentModel.Primitives','System.ComponentModel.TypeConverter')+@(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll' | Select-Object -ExpandProperty FullName)
Add-Type -Path "$PSScriptRoot/BuildCustomerComments.cs" -ReferencedAssemblies $refs
[BuildCustomerComments]::Run($pack)
$template=[IO.File]::ReadAllText((Join-Path $project 'Assets/_Project/UI/Assets/ActionButtonSpritePackV1/Components/PopoverPanel.png.meta'))
$entries=@()
foreach($mood in @('Happy','Neutral','Angry')) {
 foreach($kind in @('Panel','Tail','Badge')) {
  $name="Comment$mood$kind"; $border=if($kind-eq'Panel'){@(80,80,80,80)}else{@(0,0,0,0)}
  $guid=[guid]::NewGuid().ToString('N');$meta=$template -replace '(?m)^guid: .*',('guid: '+$guid) -replace '(?m)^  spriteBorder: .*',"  spriteBorder: {x: $($border[0]), y: $($border[1]), z: $($border[2]), w: $($border[3])}"
  $file="$pack/Components/$name.png.meta";if(Test-Path -LiteralPath $file){$guid=[regex]::Match([IO.File]::ReadAllText($file),'(?m)^guid: (\w+)').Groups[1].Value;$meta=$meta -replace '(?m)^guid: .*',('guid: '+$guid)}
  [IO.File]::WriteAllText($file,$meta)
  $i=@('Happy','Neutral','Angry').IndexOf($mood)
  $x=if($kind-eq'Panel'){16}elseif($kind-eq'Tail'){560}else{720};$y=16+$i*$(if($kind-eq'Panel'){272}else{144})
  $w=if($kind-eq'Panel'){512}else{128};$h=if($kind-eq'Panel'){256}elseif($kind-eq'Tail'){96}else{128}
  $entries+=@{name=$name;guid=$guid;file="Components/$name.png";rect=@{x=$x;y=1024-$y-$h;width=$w;height=$h};border=$border;pivot=@(.5,.5)}
 }
}
[IO.File]::WriteAllText("$pack/CustomerCommentsV1.json",(@{version=1;atlas='CustomerCommentsV1.png';width=1024;height=1024;origin='Unity bottom-left';sprites=$entries}|ConvertTo-Json -Depth 6))
$atlasTemplate=[IO.File]::ReadAllText((Join-Path $project 'Assets/_Project/UI/Assets/ActionButtonSpritePackV1/ActionButtonsV1.png.meta'))
$prefix=($atlasTemplate -split '  spriteSheet:')[0] -replace '(?m)^guid: .*',('guid: '+[guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath "$pack/CustomerCommentsV1.png.meta") {
 $guid=[regex]::Match([IO.File]::ReadAllText("$pack/CustomerCommentsV1.png.meta"),'(?m)^guid: (\w+)').Groups[1].Value
 $prefix=$prefix -replace '(?m)^guid: .*',('guid: '+$guid)
}
$sheet="  spriteSheet:`n    serializedVersion: 2`n    sprites:`n";$index=0
foreach($entry in $entries){$r=$entry.rect;$b=$entry.border;$id=[guid]::NewGuid().ToString('N');$fileID=21300000+$index*2;$sheet+=@"
    - serializedVersion: 2
      name: $($entry.name)
      rect:
        serializedVersion: 2
        x: $($r.x)
        y: $($r.y)
        width: $($r.width)
        height: $($r.height)
      alignment: 0
      pivot: {x: 0.5, y: 0.5}
      border: {x: $($b[0]), y: $($b[1]), z: $($b[2]), w: $($b[3])}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: $id
      internalID: $fileID
      vertices: []
      indices:
      edges: []
      weights: []

"@;$index++}
$sheet+="    outline: []`n    physicsShape: []`n    bones: []`n    spriteID: $([guid]::NewGuid().ToString('N'))`n    internalID: 0`n    vertices: []`n    indices:`n    edges: []`n    weights: []`n    secondaryTextures: []`n    nameFileIdTable:`n"
for($i=0;$i-lt$entries.Count;$i++){$sheet+="      $($entries[$i].name): $(21300000+$i*2)`n"}
[IO.File]::WriteAllText("$pack/CustomerCommentsV1.png.meta",$prefix+$sheet+"  mipmapLimitGroupName:`n  pSDRemoveMatte: 0`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n")
foreach($folder in @($pack,"$pack/Components")){if(!(Test-Path -LiteralPath ($folder+'.meta'))){[IO.File]::WriteAllText($folder+'.meta',"fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`n")}}
Write-Output 'Generated 9 blank comment components and a named 1024px sprite sheet.'
