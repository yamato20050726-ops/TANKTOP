$bytes = [System.IO.File]::ReadAllBytes('Assets/Fonts/KatasumiDotHutoji.ttf')
$numTables = ($bytes[4] * 256) + $bytes[5]
Write-Output "numTables: $numTables"
for ($i = 0; $i -lt $numTables; $i++) {
    $offset = 12 + $i * 16
    $tag = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 4)
    if ($tag -eq 'cmap') {
        $b = $bytes[($offset+8)..($offset+11)]
        $co = ($b[0] * 16777216) + ($b[1] * 65536) + ($b[2] * 256) + $b[3]
        Write-Output "cmap at: $co"
        $sub = ($bytes[$co+2] * 256) + $bytes[$co+3]
        Write-Output "subtables: $sub"
        for ($s = 0; $s -lt $sub; $s++) {
            $p = ($bytes[$co+4+$s*8] * 256) + $bytes[$co+5+$s*8]
            $e = ($bytes[$co+6+$s*8] * 256) + $bytes[$co+7+$s*8]
            $sb = $bytes[($co+8+$s*8)..($co+11+$s*8)]
            $so = ($sb[0] * 16777216) + ($sb[1] * 65536) + ($sb[2] * 256) + $sb[3]
            $f = ($bytes[$co+$so] * 256) + $bytes[$co+$so+1]
            Write-Output "sub ${s} plat=${p} enc=${e} offset=${so} format=${f}"
            
            if ($f -eq 4) {
                $segCountX2 = ($bytes[$co+$so+6] * 256) + $bytes[$co+$so+7]
                $segCount = [int]($segCountX2 / 2)
                $endOffset = $co + $so + 14
                $startOffset = $endOffset + $segCount * 2 + 2
                
                $chars = @{}
                for ($seg = 0; $seg -lt $segCount; $seg++) {
                    $end = ($bytes[$endOffset + $seg*2] * 256) + $bytes[$endOffset + $seg*2 + 1]
                    $start = ($bytes[$startOffset + $seg*2] * 256) + $bytes[$startOffset + $seg*2 + 1]
                    if ($start -ne 65535) {
                        for ($c = $start; $c -le $end; $c++) {
                            $chars[$c] = $true
                        }
                    }
                }
                Write-Output "Format 4 char count: $($chars.Count)"
                $test = "0123456789/ XPxp+-=／　"
                for ($k=0; $k -lt $test.Length; $k++) {
                    $ch = $test[$k]
                    $code = [int][char]$ch
                    $hex = $code.ToString('X4')
                    $has = $chars.ContainsKey($code)
                    Write-Output "Char '$ch' (U+$hex): $has"
                }
            }
        }
    }
}

