Add-Type -AssemblyName System.Drawing

function New-PlaceholderImage {
    param(
        [string]$Label,
        [int]$WidthPx,
        [int]$HeightPx,
        [System.Drawing.Color]$BgTop,
        [System.Drawing.Color]$BgBottom,
        [System.Drawing.Color]$AccentColor,
        [string]$OutPath,
        [string]$Format  # "jpeg" or "png"
    )

    $bmp = New-Object System.Drawing.Bitmap($WidthPx, $HeightPx)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    # --- gradient background ---
    $gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.Rectangle]::new(0, 0, $WidthPx, $HeightPx),
        $BgTop, $BgBottom,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
    )
    $g.FillRectangle($gradBrush, 0, 0, $WidthPx, $HeightPx)
    $gradBrush.Dispose()

    # slot / zone proportions (match SubjectSlotsJson and TextZonesJson)
    # title:   x=0.05 y=0.02 w=0.90 h=0.10
    # subject: x=0.25 y=0.15 w=0.50 h=0.60
    # footer:  x=0.05 y=0.78 w=0.90 h=0.20

    $titleRect   = [System.Drawing.RectangleF]::new(0.05*$WidthPx, 0.02*$HeightPx, 0.90*$WidthPx, 0.10*$HeightPx)
    $subjectRect = [System.Drawing.RectangleF]::new(0.25*$WidthPx, 0.15*$HeightPx, 0.50*$WidthPx, 0.60*$HeightPx)
    $footerRect  = [System.Drawing.RectangleF]::new(0.05*$WidthPx, 0.78*$HeightPx, 0.90*$WidthPx, 0.20*$HeightPx)

    # semi-transparent zone fills
    $zoneFill = [System.Drawing.Color]::FromArgb(40, 255, 255, 255)
    $zoneBrush = New-Object System.Drawing.SolidBrush($zoneFill)
    $g.FillRectangle($zoneBrush, $titleRect)
    $g.FillRectangle($zoneBrush, $footerRect)
    $zoneBrush.Dispose()

    # subject slot — dashed border + translucent fill
    $slotFill   = [System.Drawing.Color]::FromArgb(50, 255, 255, 255)
    $slotBrush  = New-Object System.Drawing.SolidBrush($slotFill)
    $g.FillRectangle($slotBrush, $subjectRect)
    $slotBrush.Dispose()

    $dashPen = New-Object System.Drawing.Pen($AccentColor, [Math]::Max(1, $WidthPx/210))
    $dashPen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $g.DrawRectangle($dashPen, $subjectRect.X, $subjectRect.Y, $subjectRect.Width, $subjectRect.Height)
    $dashPen.Dispose()

    # accent border
    $borderPen = New-Object System.Drawing.Pen($AccentColor, [Math]::Max(2, $WidthPx/105))
    $g.DrawRectangle($borderPen, 0, 0, $WidthPx-1, $HeightPx-1)
    $borderPen.Dispose()

    # text — title zone
    $titleFontSize = [Math]::Max(8, $WidthPx / 28)
    $titleFont = New-Object System.Drawing.Font("Georgia", $titleFontSize, [System.Drawing.FontStyle]::Italic)
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 60, 40, 40))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("In Loving Memory", $titleFont, $textBrush, $titleRect, $sf)
    $titleFont.Dispose()

    # text — subject zone label
    $slotFontSize = [Math]::Max(7, $WidthPx / 42)
    $slotFont = New-Object System.Drawing.Font("Arial", $slotFontSize)
    $gray = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(140, 60, 40, 40))
    $g.DrawString("[Subject Photo]", $slotFont, $gray, $subjectRect, $sf)
    $slotFont.Dispose()
    $gray.Dispose()

    # text — footer zone
    $footerFontSize = [Math]::Max(7, $WidthPx / 42)
    $footerFont = New-Object System.Drawing.Font("Georgia", $footerFontSize)
    $g.DrawString("Name · Date of Birth — Date of Passing`n`"Forever In Our Hearts`"", $footerFont, $textBrush, $footerRect, $sf)
    $footerFont.Dispose()
    $textBrush.Dispose()

    # theme label (watermark-style)
    $wmFontSize = [Math]::Max(6, $WidthPx / 60)
    $wmFont = New-Object System.Drawing.Font("Arial", $wmFontSize)
    $wmBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 60, 40, 40))
    $wmSf = New-Object System.Drawing.StringFormat
    $wmSf.Alignment = [System.Drawing.StringAlignment]::Far
    $wmSf.LineAlignment = [System.Drawing.StringAlignment]::Far
    $g.DrawString("[PLACEHOLDER] $Label", $wmFont, $wmBrush,
        [System.Drawing.RectangleF]::new(0, $HeightPx - $wmFontSize*2 - 4, $WidthPx - 4, $wmFontSize*2), $wmSf)
    $wmFont.Dispose()
    $wmBrush.Dispose()

    $g.Dispose()

    $dir = [System.IO.Path]::GetDirectoryName($OutPath)
    if (-not [System.IO.Directory]::Exists($dir)) { [System.IO.Directory]::CreateDirectory($dir) | Out-Null }

    if ($Format -eq "jpeg") {
        $jpegEncoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
            Where-Object { $_.MimeType -eq "image/jpeg" } | Select-Object -First 1
        $encParams = New-Object System.Drawing.Imaging.EncoderParameters(1)
        $encParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
            [System.Drawing.Imaging.Encoder]::Quality, 88L)
        $bmp.Save($OutPath, $jpegEncoder, $encParams)
    } else {
        $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $bmp.Dispose()
    Write-Host "  Created: $OutPath ($WidthPx x $HeightPx)"
}

$outDir = "$PSScriptRoot\..\src\DesignSystem.Api\storage\backgrounds\seeded"

Write-Host "`n=== Serene Lily (A3, lavender) ==="
New-PlaceholderImage -Label "Serene Lily" `
    -WidthPx 420 -HeightPx 594 `
    -BgTop    ([System.Drawing.Color]::FromArgb(230, 220, 240)) `
    -BgBottom ([System.Drawing.Color]::FromArgb(245, 235, 255)) `
    -AccentColor ([System.Drawing.Color]::FromArgb(160, 120, 180)) `
    -OutPath "$outDir\lily_preview.jpg" -Format "jpeg"

New-PlaceholderImage -Label "Serene Lily" `
    -WidthPx 840 -HeightPx 1188 `
    -BgTop    ([System.Drawing.Color]::FromArgb(230, 220, 240)) `
    -BgBottom ([System.Drawing.Color]::FromArgb(245, 235, 255)) `
    -AccentColor ([System.Drawing.Color]::FromArgb(160, 120, 180)) `
    -OutPath "$outDir\lily_source.png" -Format "png"

Write-Host "`n=== Golden Autumn (A3, amber) ==="
New-PlaceholderImage -Label "Golden Autumn" `
    -WidthPx 420 -HeightPx 594 `
    -BgTop    ([System.Drawing.Color]::FromArgb(255, 240, 200)) `
    -BgBottom ([System.Drawing.Color]::FromArgb(240, 200, 120)) `
    -AccentColor ([System.Drawing.Color]::FromArgb(180, 110, 30)) `
    -OutPath "$outDir\autumn_preview.jpg" -Format "jpeg"

New-PlaceholderImage -Label "Golden Autumn" `
    -WidthPx 840 -HeightPx 1188 `
    -BgTop    ([System.Drawing.Color]::FromArgb(255, 240, 200)) `
    -BgBottom ([System.Drawing.Color]::FromArgb(240, 200, 120)) `
    -AccentColor ([System.Drawing.Color]::FromArgb(180, 110, 30)) `
    -OutPath "$outDir\autumn_source.png" -Format "png"

Write-Host "`n=== Peaceful Dove (A4, sky blue) ==="
New-PlaceholderImage -Label "Peaceful Dove" `
    -WidthPx 400 -HeightPx 566 `
    -BgTop    ([System.Drawing.Color]::FromArgb(220, 235, 250)) `
    -BgBottom ([System.Drawing.Color]::FromArgb(245, 248, 255)) `
    -AccentColor ([System.Drawing.Color]::FromArgb(100, 140, 200)) `
    -OutPath "$outDir\dove_preview.jpg" -Format "jpeg"

New-PlaceholderImage -Label "Peaceful Dove" `
    -WidthPx 800 -HeightPx 1132 `
    -BgTop    ([System.Drawing.Color]::FromArgb(220, 235, 250)) `
    -BgBottom ([System.Drawing.Color]::FromArgb(245, 248, 255)) `
    -AccentColor ([System.Drawing.Color]::FromArgb(100, 140, 200)) `
    -OutPath "$outDir\dove_source.png" -Format "png"

Write-Host "`nDone."
