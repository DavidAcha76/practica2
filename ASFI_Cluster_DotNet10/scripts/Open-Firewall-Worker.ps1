param([int]$Port = 5201)
$rule = "ASFI Worker $Port"
if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -DisplayName $rule -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
}
Write-Host "Firewall listo para TCP $Port" -ForegroundColor Green
