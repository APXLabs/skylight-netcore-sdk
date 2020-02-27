$path = ".\coverage.runsettings"
$outpath = ".\coverage-auto.runsettings"
((Get-Content $path) -join "") | Set-Content -NoNewline $outpath
Remove-Item .\TestResults -Recurse -ErrorAction Ignore
dotnet test --collect:"XPlat Code Coverage" --settings:coverage-auto.runsettings -v n
C:\Users\andrew.sugaya\.nuget\packages\reportgenerator\4.4.7\tools\net47\ReportGenerator.exe "-reports:TestResults\**\coverage.cobertura.xml" "-targetdir:coveragereport" -reporttypes:Html