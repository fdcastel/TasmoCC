# choco install mongodb.portable -y
# netsh advfirewall firewall delete rule name="Open mongod port 27017"
# netsh advfirewall firewall add rule name="Open mongod port 27017" dir=in action=allow protocol=TCP localport=27017

$host.UI.RawUI.WindowTitle = 'TasmoCC MongoDb server'

Write-Host "Starting MongoDb server..." -ForegroundColor Yellow
$serverJob = Start-Job -Name 'Database server' {
    $port = 27017

    $dbFolder = './data/db'
    $logsFolder = './data/logs'

    mkdir -force $dbFolder | Out-Null
    mkdir -force ./data/logs | Out-Null

    & mongod --dbpath $dbFolder --port $port --logappend --logpath "$logsFolder/mongod.log" --bind_ip 127.0.0.1 --replSet rs0 --oplogSize 64 --verbose
}

$database = 'localhost/tasmocc'

$initFile = '.\docker\mongodb-init.js'
if (Test-Path $initFile) {
    Write-Host "Initializing database..." -ForegroundColor Yellow
    mongo $database $initFile
    Write-Host "Ready!" -ForegroundColor Yellow
}

$serverJob | Receive-Job -AutoRemoveJob -Wait
