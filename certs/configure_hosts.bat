@echo off
set "hostsFilePath=%SystemRoot%\System32\drivers\etc\hosts"
set "entryToAdd=127.0.0.1 ids.ziralink.com"

echo Adding entry to hosts file: %entryToAdd%

echo %entryToAdd% >> "%hostsFilePath%"

echo Entry added successfully.

pause