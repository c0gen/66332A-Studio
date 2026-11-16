Option Explicit

Dim files, shell, workspace, preview, studio, powershell, command, exitCode
Set files = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
workspace = files.GetParentFolderName(WScript.ScriptFullName)
preview = files.BuildPath(workspace, "artifacts\66332A Studio Preview")
studio = files.BuildPath(preview, "Agilent66332A.Desktop.exe")

If files.FileExists(studio) Then
    shell.CurrentDirectory = preview
    exitCode = Launch(Quote(studio), 1, False)
Else
    powershell = shell.ExpandEnvironmentStrings("%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe")
    command = Quote(powershell) & " -NoLogo -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File " & _
        Quote(files.BuildPath(workspace, "tools\dev.ps1"))
    exitCode = Launch(command, 0, True)
    If exitCode <> 0 Then
        MsgBox "The development build exited with code " & exitCode & "." & vbCrLf & _
            "Run tools\dev.ps1 from PowerShell to see the details.", vbExclamation, "66332A Studio"
    End If
End If
WScript.Quit exitCode

Function Quote(path)
    Quote = Chr(34) & path & Chr(34)
End Function

Function Launch(commandLine, windowStyle, waitForExit)
    Dim message
    On Error Resume Next
    Launch = shell.Run(commandLine, windowStyle, waitForExit)
    If Err.Number <> 0 Then
        message = Err.Description
        On Error GoTo 0
        MsgBox message, vbExclamation, "Could not start 66332A Studio"
        WScript.Quit 1
    End If
End Function
