# SmallerImages

If you want to reduce the file size of images uploaded to your Umbraco website, then this is the package for you. You can set a maximum width and height, this package will replace the original image with a smaller cropped version. It also allows you to create another crop of the image, perhaps a smaller preview size image.

To use this in your Umbraco website, the best way to install it is using NuGet:

[![Nuget Downloads](https://img.shields.io/nuget/dt/SmallerImages.svg)](https://www.nuget.org/packages/SmallerImages)

```js
Install-Package SmallerImages
```

Then just add these app settings to your web.config file and edit the values accordingly:

```xml
<add key="ImageResizeWidth" value="1920" />
<add key="ImageResizeHeight" value="1080" />
<add key="ImageResizeSuffix" value="1080p" />
<add key="ImageResizeKeepOriginal" value="false" />
<add key="ImageResizeUpscale" value="false" />
<add key="ImageResizePreviewWidth" value="240" />
<add key="ImageResizePreviewHeight" value="136" />
<add key="ImageResizePreviewSuffix" value="_preview" />
<add key="ImageResizeMaintainRatio" value="false" />
<add key="ImageResizeApplyToExistingImages" value="true" />
```

## FAQs

  - ### Does it work with existing images?
    Yes it does, you can enable it by setting this appSetting value to `true`
    ```xml
    <add key="ImageResizeApplyToExistingImages" value="true" />
    ```
  - ### How do I turn off the preview image crop?
    Change these config settings to the values as they are below:
    ```xml
    <add key="ImageResizePreviewWidth" value="0" />
    <add key="ImageResizePreviewHeight" value="0" />
    <add key="ImageResizePreviewSuffix" value="" />
    ```
  - ### What happens to the original image?
    It's completely up to you. If you don't want to keep it you can set this value to false, otherwise set it to true.
    
    ```xml
    <add key="ImageResizeKeepOriginal" value="false" />
    ```
## Special Thanks

This project would not exist if it wasn't for [James South](https://github.com/JimBobSquarePants) creating ImageProcessor
