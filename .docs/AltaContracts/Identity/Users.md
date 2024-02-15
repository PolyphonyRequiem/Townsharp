# User Identity & Authentication

## Generating a SHA512 hash of your password.

Using powershell:

```
$plaintextpass = "whateveryourpasswordis"
$passstream = [IO.MemoryStream]::new([byte[]][char[]]$plaintextpass)
(Get-FileHash -InputStream $passstream -Algorithm SHA512).Hash.ToLower()
```