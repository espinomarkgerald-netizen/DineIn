$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$pack = Join-Path $project 'Assets/_Project/UI/Assets/RestaurantControlsSpritePackV1'
foreach ($name in @('GreetCustomerApproved', 'SeatCustomerApproved')) {
    $sourceMeta = Join-Path $pack "Sources/$name.png.meta"
    if (!(Test-Path -LiteralPath $sourceMeta)) {
        $sourceTemplate = [IO.File]::ReadAllText((Join-Path $pack 'Sources/IconsGenerated.png.meta'))
        [IO.File]::WriteAllText($sourceMeta, ($sourceTemplate -replace '(?m)^guid: .*', ('guid: ' + [guid]::NewGuid().ToString('N'))))
    }
}
Add-Type -AssemblyName System.Drawing
$refs = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location,
    'System.Runtime', 'System.ComponentModel.Primitives', 'System.ComponentModel.TypeConverter') +
    @(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows*.dll' | Select-Object -ExpandProperty FullName)
Add-Type -Path (Join-Path $PSScriptRoot 'BuildReceptionActionIcons.cs') -ReferencedAssemblies $refs
[BuildReceptionActionIcons]::Run($pack)

$components = Join-Path $pack 'Components'
$template = [IO.File]::ReadAllText((Join-Path $components 'IconWineRefill.png.meta'))
$entries = @(
    [ordered]@{ name = 'IconGreetCustomer'; x = 704; y = 1744; fileID = 21300072 },
    [ordered]@{ name = 'IconSeatCustomer'; x = 976; y = 1744; fileID = 21300074 }
)
foreach ($entry in $entries) {
    $assetMeta = Join-Path $components ($entry.name + '.png.meta')
    if (Test-Path -LiteralPath $assetMeta) {
        $guid = [regex]::Match([IO.File]::ReadAllText($assetMeta), '(?m)^guid: (\w+)').Groups[1].Value
        $spriteId = [regex]::Match([IO.File]::ReadAllText($assetMeta), '(?m)^    spriteID: (\w+)').Groups[1].Value
    } else {
        $guid = [guid]::NewGuid().ToString('N')
        $spriteId = [guid]::NewGuid().ToString('N')
        $metaText = $template -replace '(?m)^guid: .*', ('guid: ' + $guid) `
            -replace '(?m)^    spriteID: .*', ('    spriteID: ' + $spriteId)
        [IO.File]::WriteAllText($assetMeta, $metaText)
    }
    $entry.guid = $guid
    $entry.spriteID = $spriteId
}

$manifestPath = Join-Path $pack 'RestaurantControlsV1.json'
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable
$manifest.integration = 'Reception icons are bound to the existing Greet Customer / Seat Table presentation; action handlers and reservation management are unchanged.'
foreach ($entry in $entries) {
    if (@($manifest.sprites | Where-Object { $_.name -eq $entry.name }).Count -eq 0) {
        $manifest.sprites += [ordered]@{
            name = $entry.name
            file = "Components/$($entry.name).png"
            topLeft = [ordered]@{ x = $entry.x; y = $entry.y; width = 256; height = 256 }
            unityRect = [ordered]@{ x = $entry.x; y = 48; width = 256; height = 256 }
            border = [ordered]@{ left = 0; bottom = 0; top = 0; right = 0 }
            pivot = @(0.5, 0.5)
            imageType = 'Simple'
            fileID = $entry.fileID
            spriteID = $entry.spriteID
        }
    }
}
if (@($manifest.uses | Where-Object { $_.area -eq 'Reception actions' }).Count -eq 0) {
    $manifest.uses += [ordered]@{
        icons = @('IconGreetCustomer', 'IconSeatCustomer')
        behavior = 'Greet and seat presentation states; the existing action handlers remain authoritative'
        area = 'Reception actions'
    }
}
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 30) + "`n")

$atlasMetaPath = Join-Path $pack 'RestaurantControlsV1.png.meta'
$atlasMeta = [IO.File]::ReadAllText($atlasMetaPath)
foreach ($entry in $entries) {
    if ($atlasMeta -match "(?m)^      name: $($entry.name)$") { continue }
    $spriteNode = @"
    - serializedVersion: 2
      name: $($entry.name)
      rect:
        serializedVersion: 2
        x: $($entry.x)
        y: 48
        width: 256
        height: 256
      alignment: 0
      pivot: {x: 0.5, y: 0.5}
      border: {x: 0, y: 0, z: 0, w: 0}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: $($entry.spriteID)
      internalID: $($entry.fileID)
      vertices: []
      indices:
      edges: []
      weights: []

"@
    $insertAt = $atlasMeta.LastIndexOf("    outline: []")
    if ($insertAt -lt 0) { throw 'Could not find the root sprite-sheet boundary in the atlas importer metadata.' }
    $atlasMeta = $atlasMeta.Insert($insertAt, $spriteNode)
}
foreach ($entry in $entries) {
    if ($atlasMeta -notmatch "(?m)^      $($entry.name):") {
        $atlasMeta = [regex]::Replace($atlasMeta, '(?m)^    nameFileIdTable:\r?\n',
            "    nameFileIdTable:`n      $($entry.name): $($entry.fileID)`n", 1)
    }
}
[IO.File]::WriteAllText($atlasMetaPath, $atlasMeta)

$preview = Join-Path $pack 'ReceptionActionsV1.preview.png'
if (!(Test-Path -LiteralPath ($preview + '.meta'))) {
    $previewGuid = [guid]::NewGuid().ToString('N')
    $pngMeta = $template -replace '(?m)^guid: .*', ('guid: ' + $previewGuid)
    [IO.File]::WriteAllText($preview + '.meta', $pngMeta)
}
Write-Output 'Generated matching Greet Customer and Seat Customer pictograms, appended named atlas sprites, and refreshed the manifest.'
