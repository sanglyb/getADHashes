# getADHashes

`getADHashes` is a tool designed for quickly retrieving hashes of all active users from Active Directory. The tool should be run on a domain controller with administrator privileges. After execution, a file named `users.csv` will be saved in the "result" subfolder, containing hashes of all enabled AD users.

Compiled executable is available at [bin\release folder](https://github.com/sanglyb/getADHashes/blob/main/bin/Release/getADHashes.exe).

In essence, this program provides an implementation and automation of the steps required to obtain these hashes:

The method involves extracting password hashes of active users from the domain controller's ntds with the help of [DSInternals](https://github.com/MichaelGrafnetter/DSInternals) module.

## Instructions for those who either don't want to or aren't allowed to use my app:

### 1. Execute these commands on the domain controller in PowerShell. Ensure that DSInternals is placed in the desired directory:
```powershell
$path="C:\New folder\"
cd $path
$vss=$null
$vss=Get-CimInstance -ClassName Win32_ShadowCopy -Property * | Select-Object DeviceObject,ID
vssadmin create shadow /for=C:
$vss=Get-CimInstance -ClassName Win32_ShadowCopy -Property * | Select-Object DeviceObject,ID
$vss[0]
```
### 2. Execute the following command in CMD. Ensure you update the shadow copy number (e.g., HarddiskVolumeShadowCopy[317]) based on the number displayed on your screen:
```cmd
copy \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy317\Windows\NTDS "C:\new folder"
```
### 3. Switch back to PowerShell and continue with:
```powershell
vssadmin.exe delete shadows /shadow="$($vss[0].ID)" /quiet
esentutl /r edb /d
import-module -name .\dsinternals
$key=Get-BootKey -Online
$dump=Get-ADDBAccount -all -DBPath '.\ntds.dit' -BootKey $key | Where-Object {$_.enabled -eq "True"}
$dump | where-object {$_.samaccounttype -eq "user"} | Format-Custom -View PwDump | out-file -FilePath users.pwdump -Encoding utf8
remove-item *edb*
remove-item *ntds*
```
**Note**: After copying the hashes, ensure you remove them from the directory to maintain security.

## License
This program is licensed under the MIT license.

For more insights and articles, visit my blog at [MyTechNote](https://www.mytechnote.ru).
