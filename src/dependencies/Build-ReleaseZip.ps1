function Create-Directory {
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [string]$Path
    )

    if(![System.IO.Directory]::Exists($Path)) {
        [void][System.IO.Directory]::CreateDirectory($Path);
        while(![System.IO.Directory]::Exists($Path)) {
            Start-Sleep -Milliseconds 5;
        }
    }
}

#Setup paths
$current = New-Object System.IO.DirectoryInfo($PSScriptRoot);
$root = $current.Parent;
$binsrc = [System.IO.Path]::Combine($root.FullName, "AmsiScanner", "bin", "release", "net6.0");
$release = [System.IO.Path]::Combine($root.FullName, "release");
$samples_src = [System.IO.Path]::Combine($current.FullName, "samples");
$samples_dst = [System.IO.Path]::Combine($release, "samples");
$zip = [System.IO.Path]::Combine($root.FullName, "release.zip");

#Delete old contents
Remove-Item $release -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zip -Recurse -Force -ErrorAction SilentlyContinue

#Ensure folders exist
Create-Directory $release
Create-Directory $samples_dst

#Copy binaries over
$files = @("AmsiScanner.Common.dll", "AmsiScanner.exe", "AmsiScanner.dll", "AmsiScanner.runtimeconfig.json", "System.CommandLine.dll", "System.CommandLine.NamingConventionBinder.dll");
foreach($file in $files) {
    Copy-Item ([System.IO.Path]::Combine($binsrc, $file)) $release -Force;
}

#Copy System.Management.Automation.dll
$SMAPath = [System.IO.Path]::Combine($root.FullName, "AmsiScanner", "bin", "Release", "net6.0", "runtimes", "win", "lib", "net6.0", "System.Management.Automation.dll")
Copy-Item $SMAPath $release -Force
#"C:\Users\helpdesk\Desktop\Workspace\AmsiScanner\src\AmsiScanner\bin\Release\net6.0\runtimes\win\lib\net6.0\System.Management.Automation.dll"

#Copy samples over
$files = gci $samples_src -File
foreach($file in $files) {
    Copy-Item $file.FullName $samples_dst
}

#Compress the release folder and save it
Get-ChildItem -Path $release | Compress-Archive -DestinationPath $zip -CompressionLevel Optimal -Force;