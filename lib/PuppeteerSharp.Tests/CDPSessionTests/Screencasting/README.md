# Screencasting

https://chromedevtools.github.io/devtools-protocol/

https://github.com/hardkoded/puppeteer-sharp/issues/2943

# To do

## Resolve "Error CS8002 : Referenced assembly 'SharpAvi, Version=3.0.1.0, Culture=neutral, PublicKeyToken=null' does not have a strong name."

I added this in the interim

```diff
$ git diff -- lib/PuppeteerSharp.Tests/PuppeteerSharp.Tests.csproj
diff --git a/lib/PuppeteerSharp.Tests/PuppeteerSharp.Tests.csproj b/lib/PuppeteerSharp.Tests/PuppeteerSharp.Tests.csproj
index 5becc38d..320ac2c4 100644
--- a/lib/PuppeteerSharp.Tests/PuppeteerSharp.Tests.csproj
+++ b/lib/PuppeteerSharp.Tests/PuppeteerSharp.Tests.csproj
@@ -45,4 +48,7 @@
        <ItemGroup>
          <None Remove="TestExpectations\TestExpectations.json" />
        </ItemGroup>
+  <PropertyGroup>
+    <NoWarn>8002</NoWarn>
+  </PropertyGroup>
 </Project>
```

# Troubleshooting

## WerFault.exe is still running after tests finish

![Screenshot of dialog saying WerFault.exe is still running after tests finish](./assets/orphaned-process-dialog.png)
