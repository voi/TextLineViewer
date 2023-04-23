@set MYPATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319
@set PATH=%MYPATH%;%PATH%
@csc.exe /t:winexe /optimize+ /out:TextLineViewer.exe TextLineViewer.cs /r:system.dll,system.drawing.dll,system.windows.forms.dll,system.io.dll,System.Reflection.dll
@set MYPATH=
