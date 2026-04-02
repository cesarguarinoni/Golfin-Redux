Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

repo = fso.GetParentFolderName(WScript.ScriptFullName)
command = "cmd /c cd /d """ & repo & """ && node scripts\stop-app.mjs"

shell.Run command, 0, False
