param([int]$LetterSpacing = 25, [int]$SepSpacing = 70)
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "TVClock Modern - 設定")
$dlg = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $nameCond)
if ($null -eq $dlg) {
  Write-Host "DIALOG-NOT-FOUND"
  exit 1
}
Write-Host "dialog found"

function Set-Slider([string]$sliderName, [double]$val) {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $sliderName)
  $s = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if ($null -eq $s) {
    Write-Host "slider $sliderName NOT-FOUND"
    return
  }
  $p = $s.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
  $p.SetValue($val)
  Write-Host "set $sliderName = $val"
}

Set-Slider "LetterSpacingSlider" $LetterSpacing
Set-Slider "SeparatorSpacingSlider" $SepSpacing

$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "適用")
$btn = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if ($null -ne $btn) {
  $ip = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
  $ip.Invoke()
  Write-Host "applied"
}
else {
  Write-Host "apply button NOT-FOUND"
}
