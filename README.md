#consul-ps-provider

consul-ps-provider - a PowerShell provider for the KV store of Consul (http://consul.io).

This is an early release, but has enough functionality to get started. This was our first PowerShell Provider so any feedback or suggestions on how to do things better are welcome (pull requests OR issues) 

## Features
So as far as the PowerShell Provider hierarchy goes, this is the functionality implemented (or not implemented)

### General Consul support
#### Implemented
* http/https connections to consul
* Read string (utf8) and binary/byte[] values

#### Not Yet Implemented
* Basic Auth for http connections
* Flags on keys
* Auth Tokens
* Writing binary (byte[]) values. Only string (utf8) values can be written.


### DriveCmdletProvider Support

#### New-PSDrive
Creating a new Consul named PSDrive:

```PowerShell
PS C:\> New-PSDrive CONSUL ConsulProvider http://localhost:8500

Name        Used (GB)     Free (GB) Provider      Root                                                                        CurrentLocation
----        ---------     --------- --------      ----                                                                        ---------------
CONSUL                              ConsulProv... http://localhost:8500

PS C:\> cd CONSUL:
PS CONSUL:\>
```

The connection root for the drive should include the base URI for the consul node (or VIP) to connect to including protocol, host, and port. The rest of the path to the KV store will be added automatically.

### Item Cmdlets Support

#### Set-Item
Set key/values using the Set-Item cmdlet.
```PowerShell
PS CONSUL:\> Set-Item test\foo bar
PS CONSUL:\>
```

#### Test-Path
Check for existence of a folder or leaf (key/value)
```PowerShell
PS CONSUL:\> Test-Path test
True
PS CONSUL:\> Test-Path test\foo
True
PS CONSUL:\> Test-Path tee
False
PS CONSUL:\>
```

#### Get-Item
Get-Item retrieves an item from Consul including all the item metadata and its value. 
```PowerShell
PS CONSUL:\> Get-Item test\foo

PSPath        : ConsulPSProvider\ConsulProvider::http:\\localhost:8500\test\foo
PSParentPath  : ConsulPSProvider\ConsulProvider::http:\\localhost:8500\test
PSChildName   : foo
PSDrive       : CONSUL
PSProvider    : ConsulPSProvider\ConsulProvider
PSIsContainer : False
ValueAsString : bar
CreateIndex   : 52071
Flags         : 0
Key           : test/foo
Value         : {98, 97, 114}

PS CONSUL:\> 
```

The returned object has a `ValueAsString` property which uses UTF8 encoding to decode the byte[] value into a string. The raw byte[] is available in the Value field.

#### Remove-Item
You can delete a key or an entire tree of keys using the Remove-Item cmdlet

```PowerShell
PS CONSUL:\> Remove-Item test\foo
PS CONSUL:\> Remove-Item test -recurse
PS CONSUL:\> 
```

#### Not Implemented
* Clear-Item
* Copy-Item
* Invoke-Item
* Move-Item
* Rename-Item
* any Item Property cmdlets
* any Item Content cmdlets

### Container/Navigation Cmdlets
There is enough basic support for containers/navigation to move around in the consul tree and list keys, create containers and items.

```PowerShell
PS CONSUL:\test> cd container
PS CONSUL:\test\container> ls
PS CONSUL:\test\container> set-item foo bar
PS CONSUL:\test\container> mkdir sub
PS CONSUL:\test\container> ls
foo
sub/
PS CONSUL:\test\container> cd sub
PS CONSUL:\test\container\sub> ls
PS CONSUL:\test\container\sub> cd ..
PS CONSUL:\test\container> ls
foo
sub/
PS CONSUL:\test\container>
```

#### Not Implemented
* Copy-Item
* Move-Item

## Building and Installing

1. Clone the repository.
2. Build in VisualStudio 
  * Note there are two NuGet dependencies, one for the System.Management.Automation assembly for the PowerShell types, and one for Consul.NET for interaction wtih Consul
3. In a PowerShell session, navigate to the bin directory built in prior step and run either:
  * InstallProvider.ps1
  * Import-Module .\ConsulPSProvider.dll

### Debugging

If you'd like to debug the project in VisualStudio, go into the project properties -> Debug tab
1. Set Start Action -> External Program -> Browse or enter path to PowerShell (usually `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`)
2. Set Command Line Arguments to `-NoExit  -File .\InstallProvider.ps1`

Start debugging, it should open a PowerShell command session and import the module. 

## Constributing

We would be glad to receive Pull Requests and/or Issues. We will make an effort to resolve issues in a timely manner, but do not have time dedicated to this project.

## License

This project ( consul-ps-provider ) is released under Apache 2.0 License ( see LICENSE ) 
Copyright (c) 2015 Ntent

The code and functionality in this project comes with no warranty, guarantees or support. It has been tested a fair amount and works well for us, however by using it, you do so at your own risk!
