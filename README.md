# TextLineViewer

sample of C# Windows form application.
TextLineViewer is text file viewer to show list of each line.

## A command line parmeter.

~~~
TextLineViewer.exe [config-file-path]
~~~

## A format of `[config-file-path]` is xml.

~~~xml
<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<configure>
    <content encoding="utf-8" path="sample.cs" />
    <formats>
        <format pattern="^.*ERROR:\s(.*)" format="⚠️ $1" />
        <format pattern="^.*TODO:\s(.*)" format="📌 $1" />
    </formats>
    <action process="test.exe" args="-Text {0} -New" />
</configure>
~~~

*	`content` element
	-	`encoding` attribute : attribute value is code-page or code-name.(ref: https://learn.microsoft.com/ja-jp/dotnet/api/system.text.encoding?view=net-7.0#list-of-encodings)
	-	`path` attribute
*	`formats` element : setting of line format to display.
	*	`format` element
		-	`pattern` attribute
		-	`format` attribute 
*	`action` element : application of 
	-	`process` attribute
	-	`args` attribute
