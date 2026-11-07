$ErrorActionPreference='Stop';Add-Type -AssemblyName System.Drawing
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root="$project/Assets/_Project/UI/Assets/ReceptionNewspaperSpritePackV1";$dir="$root/Surfaces";New-Item -ItemType Directory -Force -Path $dir|Out-Null
$fonts="$root/Fonts";New-Item -ItemType Directory -Force -Path $fonts|Out-Null
$fontPath="$fonts/NewspaperBody.asset";$fontMeta=$fontPath+'.meta'
$fontGuid=if(Test-Path -LiteralPath $fontMeta){[regex]::Match([IO.File]::ReadAllText($fontMeta),'(?m)^guid: (\w+)').Groups[1].Value}else{[guid]::NewGuid().ToString('N')}
# The existing regular Atkinson atlas is a legacy TextCore asset; give this private copy a TMP binding.
$font=[IO.File]::ReadAllText("$project/Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible/AtkinsonHyperlegible-Regular SDF.asset")
$font=$font.Replace('m_Script: {fileID: 19001, guid: 0000000000000000e000000000000000, type: 0}','m_Script: {fileID: 11500000, guid: 71c1514a6bd24e1e882cebbe1904ce04, type: 3}').Replace('m_FallbackFontAssetTable: []',"m_FallbackFontAssetTable:`n  - {fileID: 11400000, guid: 6e1f10b7e118faf45901758c1ae43feb, type: 2}").Replace('guid: c8d12adcee749c344b8117cf7c7eb912','guid: fe393ace9b354375a9cb14cdbbc28be4')
[IO.File]::WriteAllText($fontPath,$font.Replace('m_Name: AtkinsonHyperlegible-Regular SDF','m_Name: NewspaperBody'));[IO.File]::WriteAllText($fontMeta,"fileFormatVersion: 2`nguid: $fontGuid`nNativeFormatImporter:`n  externalObjects: {}`n  mainObjectFileID: 11400000`n")
if(!(Test-Path -LiteralPath "$fonts.meta")){[IO.File]::WriteAllText("$fonts.meta","fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`n")}
Copy-Item -LiteralPath "$project/Assets/_Project/UI/Assets/Fonts/Atkinson_Hyperlegible/OFL.txt" -Destination "$fonts/OFL.txt" -Force
$template=[IO.File]::ReadAllText("$project/Assets/_Project/UI/Assets/CustomerDrawerSpritePackV1/Components/PortraitTile.png.meta")
$specs=@(@('PaperBody',1024,640,32),@('ColumnInset',384,256,16),@('PriorityStrip',512,128,16),@('MarketRow',512,96,16),@('QuoteFrame',384,256,16),@('PartyTag',384,96,16),@('NavigationShell',256,64,16))
$entries=@()
foreach($spec in $specs){$name=$spec[0];$w=[int]$spec[1];$h=[int]$spec[2];$border=[int]$spec[3];$bmp=[Drawing.Bitmap]::new($w,$h,[Drawing.Imaging.PixelFormat]::Format32bppArgb);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::Transparent)
 $fill=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($(if($name-eq'PriorityStrip'){'#FBE8B2'}else{'#FFF4DD'})));$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#29374A'),2)
 $g.FillRectangle($fill,4,4,$w-8,$h-8);$g.DrawRectangle($pen,4,4,$w-9,$h-9);$fill.Dispose();$pen.Dispose();$g.Dispose();$bmp.Save("$dir/$name.png",[Drawing.Imaging.ImageFormat]::Png);$bmp.Dispose()
 $meta="$dir/$name.png.meta";$guid=if(Test-Path -LiteralPath $meta){[regex]::Match([IO.File]::ReadAllText($meta),'(?m)^guid: (\w+)').Groups[1].Value}else{[guid]::NewGuid().ToString('N')}
 [IO.File]::WriteAllText($meta,($template -replace '(?m)^guid: .*',"guid: $guid" -replace '(?m)^    spriteID: .*',"    spriteID: $guid" -replace '(?m)^  spriteBorder: .*',"  spriteBorder: {x: $border, y: $border, z: $border, w: $border}"))
 $entries+=@{name=$name;guid=$guid;width=$w;height=$h;border=@($border,$border,$border,$border)}
}
if(!(Test-Path -LiteralPath "$dir.meta")){[IO.File]::WriteAllText("$dir.meta","fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`n")}
[IO.File]::WriteAllText("$root/NewspaperSurfacesV1.json",($entries|ConvertTo-Json -Depth 4))
$settings="$project/Assets/_Project/Resources/CasualDining/CasualDiningPolishSettings.asset";$yaml=[IO.File]::ReadAllText($settings)
$styles=@{};foreach($match in [regex]::Matches($yaml,'(?m)^  (newspaperSize|newspaperBodyFont|newspaperColumnWeights|newspaperBodySize|newspaperSectionSize|newspaperColumnGap): .*')){$styles[$match.Groups[1].Value]=$match.Value}
$yaml=[regex]::Replace($yaml,'(?ms)^  newspaperSize:.*?(?=^  alienReporterName:)','')
$icons=Get-Content -LiteralPath "$root/ReceptionNewspaperIconsV1.json" -Raw|ConvertFrom-Json
$fields="  newspaperSize: {x: 1440, y: 840}`n  newspaperBodySize: 24`n  newspaperSectionSize: 28`n  newspaperColumnGap: 28`n  newspaperPaper: {fileID: 21300000, guid: $(($entries|Where-Object name -eq 'PaperBody').guid), type: 3}`n  newspaperInset: {fileID: 21300000, guid: $(($entries|Where-Object name -eq 'PriorityStrip').guid), type: 3}`n  newspaperIcons:`n"
$fields=$fields.Replace("  newspaperIcons:","  newspaperPartyTag: {fileID: 21300000, guid: $(($entries|Where-Object name -eq 'PartyTag').guid), type: 3}`n  newspaperNavigation: {fileID: 21300000, guid: $(($entries|Where-Object name -eq 'NavigationShell').guid), type: 3}`n  newspaperIcons:")
$fields=$fields.Replace('newspaperSize: {x: 1440, y: 840}','newspaperSize: {x: 1560, y: 930}').Replace('  newspaperBodySize: 24',"  newspaperBodyFont: {fileID: 11400000, guid: 78f97b0e3ca0e3d4295c4602ecbb0baa, type: 2}`n  newspaperColumnWeights: {x: 0.29, y: 0.30, z: 0.41}`n  newspaperBodySize: 24").Replace('newspaperSectionSize: 28','newspaperSectionSize: 32')
foreach($name in $styles.Keys){$fields=[regex]::Replace($fields,"(?m)^  ${name}: .*",[System.Text.RegularExpressions.MatchEvaluator]{param($m) $styles[$name]})}
if(!$styles.ContainsKey('newspaperBodyFont') -or $styles['newspaperBodyFont'].Contains('78f97b0e3ca0e3d4295c4602ecbb0baa') -or $styles['newspaperBodyFont'].Contains('eeba7f66db197bb4fa22fd01ddc2ab5c')){$fields=[regex]::Replace($fields,'(?m)^  newspaperBodyFont: .*',"  newspaperBodyFont: {fileID: 11400000, guid: $fontGuid, type: 2}")}
foreach($e in $icons.sprites){$fields+="  - name: $($e.name)`n    sprite: {fileID: 21300000, guid: $($e.guid), type: 3}`n"}
$yaml=$yaml.Replace('  alienReporterName:', $fields+'  alienReporterName:');[IO.File]::WriteAllText($settings,$yaml)
Write-Output 'Exported seven scalable newspaper/reception surfaces and bound all 27 icons in existing polish settings.'
