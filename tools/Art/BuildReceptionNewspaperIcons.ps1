$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root=Join-Path $project 'Assets/_Project/UI/Assets'
$pack=Join-Path $root 'ReceptionNewspaperSpritePackV1'
$components=Join-Path $pack 'Components'
New-Item -ItemType Directory -Force -Path $components|Out-Null
$sources=[ordered]@{}
foreach($name in @('Approval','Cloche','Order','Guests','Happy','Neutral','Angry','Clock','Cash','Megaphone','Chef','Warning','Check')){$sources['Icon'+$name]="DayPanelSpritePackV2/Components/Icon$name.png"}
foreach($name in @('GreetCustomer','SeatCustomer','Restock')){$sources['Icon'+$name]="RestaurantControlsSpritePackV1/Components/Icon$name.png"}
$sources.IconReceipt='ActionButtonSpritePackV1/Components/IconCollectBill.png'
$sources.IconClose='ActionButtonSpritePackV1/Components/IconClose.png'
$sources.IconPrevious='CustomerDrawerSpritePackV1/Components/ChevronLeft.png'
$sources.IconNext='CustomerDrawerSpritePackV1/Components/ChevronRight.png'
$sources.StarFilled='DayPanelSpritePackV2/Components/StarFilled.png'
$sources.StarEmpty='DayPanelSpritePackV2/Components/StarEmpty.png'
foreach($e in $sources.GetEnumerator()){Copy-Item -LiteralPath (Join-Path $root $e.Value) -Destination (Join-Path $components ($e.Key+'.png')) -Force}
# Only missing small editorial glyphs are drawn; approved illustrated artwork is copied verbatim.
function New-Canvas([int]$size){[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)}
$ink=[Drawing.ColorTranslator]::FromHtml('#102543');$cream=[Drawing.ColorTranslator]::FromHtml('#FFF4DD')
foreach($name in @('IconTrendUp','IconTrendDown','IconTrendSteady','IconQuote')) {
 $b=New-Canvas 256;$g=[Drawing.Graphics]::FromImage($b);$g.Clear([Drawing.Color]::Transparent);$g.SmoothingMode='AntiAlias'
 $color=[Drawing.ColorTranslator]::FromHtml($(if($name -eq 'IconTrendUp'){'#56C75D'}elseif($name -eq 'IconTrendDown'){'#F47B68'}else{'#FFCF48'}))
 $path=[Drawing.Drawing2D.GraphicsPath]::new()
 if($name -eq 'IconQuote'){
  foreach($x in @(39,139)){
   $path.StartFigure();$path.AddBezier($x+18,51,$x-10,51,$x-10,116,$x+18,123);$path.AddLine($x+18,123,$x+31,123)
   $path.AddBezier($x+31,123,$x+29,147,$x+17,162,$x+5,176);$path.AddLine($x+5,176,$x+37,176)
   $path.AddBezier($x+37,176,$x+70,143,$x+72,111,$x+72,83);$path.AddBezier($x+72,83,$x+72,51,$x+52,51,$x+18,51);$path.CloseFigure()
  }
 }elseif($name -eq 'IconTrendSteady'){$path.AddRectangle([Drawing.Rectangle]::new(39,106,178,44))}
 else {
  $points=@(@(128,36),@(219,128),@(166,128),@(166,211),@(90,211),@(90,128),@(37,128))
  if($name -eq 'IconTrendDown'){$points=@($points|ForEach-Object {,@($_[0],(256 - [int]$_[1]))})}
  $path.AddPolygon([Drawing.PointF[]]@($points|ForEach-Object {[Drawing.PointF]::new($_[0],$_[1])}))
 }
 foreach($stroke in @(@($cream,22),@($ink,11))){$pen=[Drawing.Pen]::new($stroke[0],[single]$stroke[1]);$pen.LineJoin='Round';$g.DrawPath($pen,$path);$pen.Dispose()}
 $brush=[Drawing.SolidBrush]::new($color);$g.FillPath($brush,$path);$brush.Dispose();$path.Dispose();$g.Dispose();$b.Save("$components/$name.png",[Drawing.Imaging.ImageFormat]::Png);$b.Dispose()
}
$empty=[Drawing.Bitmap]::new("$components/StarEmpty.png");$filled=[Drawing.Bitmap]::new("$components/StarFilled.png")
$half=New-Canvas $empty.Width;$g=[Drawing.Graphics]::FromImage($half);$g.Clear([Drawing.Color]::Transparent);$g.DrawImageUnscaled($empty,0,0);$g.SetClip([Drawing.Rectangle]::new(0,0,$half.Width/2,$half.Height));$g.DrawImageUnscaled($filled,0,0);$g.Dispose();$half.Save("$components/StarHalf.png",[Drawing.Imaging.ImageFormat]::Png);$half.Dispose();$empty.Dispose();$filled.Dispose()
$atlasName='ReceptionNewspaperIconsV1';$manifestPath="$pack/$atlasName.json"
$old=if(Test-Path -LiteralPath $manifestPath){Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json}else{$null}
$template=[IO.File]::ReadAllText("$root/RestaurantControlsSpritePackV1/Components/IconWineRefill.png.meta")
$atlas=New-Canvas 2048;$g=[Drawing.Graphics]::FromImage($atlas);$g.Clear([Drawing.Color]::Transparent)
$items=@(Get-ChildItem -LiteralPath $components -Filter '*.png'|ForEach-Object {$im=[Drawing.Bitmap]::new($_.FullName);$v=[pscustomobject]@{file=$_;w=$im.Width;h=$im.Height};$im.Dispose();$v}|Sort-Object @{Expression='h';Descending=$true},@{Expression={$_.file.Name}})
$entries=@();$x=16;$y=16;$row=0;$id=if($old){[long](($old.sprites.fileID|Measure-Object -Maximum).Maximum)+2}else{21300000}
foreach($item in $items){
 $name=$item.file.BaseName;$w=$item.w;$h=$item.h
 if($x+$w+16-gt2048){$x=16;$y+=$row+16;$row=0};if($y+$h+16-gt2048){throw 'Atlas overflow'}
 $im=[Drawing.Bitmap]::new($item.file.FullName);$g.DrawImageUnscaled($im,$x,$y);$im.Dispose()
 $prior=if($old){$old.sprites|Where-Object name -eq $name|Select-Object -First 1}else{$null}
 $guid=if($prior){$prior.guid}else{[guid]::NewGuid().ToString('N')};$sid=if($prior){$prior.spriteID}else{[guid]::NewGuid().ToString('N')}
 $meta=$template -replace '(?m)^guid: .*',"guid: $guid" -replace '(?m)^  spriteMeshType: .*','  spriteMeshType: 0' -replace '(?m)^    spriteID: .*',"    spriteID: $sid"
 [IO.File]::WriteAllText($item.file.FullName+'.meta',$meta)
 $source=if($sources.Contains($name)){'Assets/_Project/UI/Assets/'+$sources[$name]}elseif($name -eq 'StarHalf'){'Composite: left StarFilled, right StarEmpty, identical bounds'}else{'Matching flat editorial glyph; tools/Art/BuildReceptionNewspaperIcons.ps1'}
 $localID=if($prior){$prior.fileID}else{$next=$id;$id+=2;$next}
 $entries+=[ordered]@{name=$name;file="Components/$name.png";guid=$guid;spriteID=$sid;fileID=$localID;source=$source;topLeft=@{x=$x;y=$y;width=$w;height=$h};rect=@{x=$x;y=2048-$y-$h;width=$w;height=$h};border=@(0,0,0,0);pivot=@(.5,.5);imageType='Simple'}
 $x+=$w+16;$row=[Math]::Max($row,$h)
}
$g.Dispose();$atlas.Save("$pack/$atlasName.png",[Drawing.Imaging.ImageFormat]::Png);$atlas.Dispose()
$atlasGuid=if($old){$old.atlasGuid}else{[guid]::NewGuid().ToString('N')}
$prefix=([IO.File]::ReadAllText("$root/RestaurantControlsSpritePackV1/RestaurantControlsV1.png.meta") -split '  spriteSheet:')[0] -replace '(?m)^guid: .*',"guid: $atlasGuid" -replace '(?m)^  spriteMeshType: .*','  spriteMeshType: 0'
$sheet="  spriteSheet:`n    serializedVersion: 2`n    sprites:`n"
foreach($e in $entries){$r=$e.rect;$sheet+=@"
    - serializedVersion: 2
      name: $($e.name)
      rect:
        serializedVersion: 2
        x: $($r.x)
        y: $($r.y)
        width: $($r.width)
        height: $($r.height)
      alignment: 0
      pivot: {x: 0.5, y: 0.5}
      border: {x: 0, y: 0, z: 0, w: 0}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: $($e.spriteID)
      internalID: $($e.fileID)
      vertices: []
      indices:
      edges: []
      weights: []

"@}
$sheet+="    outline: []`n    physicsShape: []`n    bones: []`n    spriteID: $atlasGuid`n    internalID: 0`n    vertices: []`n    indices:`n    edges: []`n    weights: []`n    secondaryTextures: []`n    nameFileIdTable:`n"
foreach($e in $entries){$sheet+="      $($e.name): $($e.fileID)`n"}
[IO.File]::WriteAllText("$pack/$atlasName.png.meta",$prefix+$sheet+"  mipmapLimitGroupName:`n  pSDRemoveMatte: 0`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n")
[IO.File]::WriteAllText($manifestPath,(@{version=1;atlas="$atlasName.png";atlasGuid=$atlasGuid;width=2048;height=2048;origin='Unity bottom-left';padding=16;labelsBaked=$false;sprites=$entries}|ConvertTo-Json -Depth 9))
foreach($folder in @($pack,$components)){if(!(Test-Path -LiteralPath ($folder+'.meta'))){[IO.File]::WriteAllText($folder+'.meta',"fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`n")}}
# Labelled contact sheet for inspection, outside production sprite imports.
$preview=New-Canvas 1600;$g=[Drawing.Graphics]::FromImage($preview);$g.Clear([Drawing.ColorTranslator]::FromHtml('#17263A'))
$font=[Drawing.Font]::new('Arial',16,[Drawing.FontStyle]::Bold);$brush=[Drawing.SolidBrush]::new($cream)
for($i=0;$i-lt$entries.Count;$i++){$e=$entries[$i];$cx=($i%6)*264;$cy=[Math]::Floor($i/6)*270;$im=[Drawing.Bitmap]::new("$components/$($e.name).png");$g.DrawImage($im,[Drawing.Rectangle]::new($cx+42,$cy+16,180,180));$im.Dispose();$g.DrawString($e.name,$font,$brush,$cx+8,$cy+210)}
$g.Dispose();$font.Dispose();$brush.Dispose();New-Item -ItemType Directory -Force -Path "$project/Documentation/UIConcepts/Newspaper"|Out-Null;$preview.Save("$project/Documentation/UIConcepts/Newspaper/ReceptionNewspaperIconsV1.preview.png",[Drawing.Imaging.ImageFormat]::Png);$preview.Dispose()
$checks=0
foreach($e in $entries){$im=[Drawing.Bitmap]::new("$components/$($e.name).png");foreach($p in @(@(0,0),@(($im.Width-1),0),@(0,($im.Height-1)),@(($im.Width-1),($im.Height-1)))){if($im.GetPixel($p[0],$p[1]).A-ne0){throw "Opaque corner: $($e.name)"};$checks++};$im.Dispose();if($e.rect.x-lt0-or$e.rect.y-lt0-or$e.rect.x+$e.rect.width-gt2048-or$e.rect.y+$e.rect.height-gt2048){throw 'Invalid rectangle'};$checks++}
foreach($s in $sources.GetEnumerator()){if((Get-FileHash -LiteralPath "$components/$($s.Key).png").Hash-ne(Get-FileHash -LiteralPath (Join-Path $root $s.Value)).Hash){throw 'Reused asset changed'};$checks++}
for($i=0;$i-lt$entries.Count;$i++){for($j=$i+1;$j-lt$entries.Count;$j++){$a=$entries[$i].rect;$b=$entries[$j].rect;if($a.x-lt$b.x+$b.width-and$a.x+$a.width-gt$b.x-and$a.y-lt$b.y+$b.height-and$a.y+$a.height-gt$b.y){throw 'Atlas overlap'};$checks++}}
if(@($entries.fileID|Select-Object -Unique).Count-ne$entries.Count){throw 'Duplicate atlas file ID'};$checks++
[IO.File]::WriteAllText("$pack/VALIDATION.txt","PASS: $checks transparency/bounds/source-integrity checks. $($entries.Count) sprites. Original reused PNGs are byte-identical. Native Unity import and live UI integration were not run.")
foreach($file in @("$atlasName.json",'README.md','VALIDATION.txt')){if(!(Test-Path -LiteralPath "$pack/$file.meta")){[IO.File]::WriteAllText("$pack/$file.meta","fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nTextScriptImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:`n")}}
Write-Output "Created $($entries.Count) sprites; $checks checks passed."
