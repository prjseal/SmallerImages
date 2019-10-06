using ImageProcessor;
using ImageProcessor.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.IO;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using Umbraco.Web;

namespace SmallerImages.Compose
{
    public class SmallerImagesComponent : IComponent
    {
        private ILogger _logger;

        public SmallerImagesComponent(ILogger logger)
        {
            _logger = logger;
        }


        /// <summary>
        /// Attach the MediaService_Saving event
        /// </summary>
        public void Initialize()
        {
            MediaService.Saving += MediaService_Saving;
        }

        /// <summary>
        /// This is called when media items are being saved. It loads the settings from the config appSettings.
        /// Depending on the settings, it create a resized version of the images and optionally replace the original files.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event arguments</param>
        private void MediaService_Saving(IMediaService sender, SaveEventArgs<IMedia> e)
        {
            var umbracoContext = DependencyResolver.Current.GetService<IUmbracoContextFactory>().EnsureUmbracoContext().UmbracoContext;

            int resizeWidth = int.Parse(WebConfigurationManager.AppSettings["ImageResizeWidth"] ?? "0");
            int resizeHeight = int.Parse(WebConfigurationManager.AppSettings["ImageResizeHeight"] ?? "0");
            string fileNameSuffix = WebConfigurationManager.AppSettings["ImageResizeSuffix"] ?? "";

            int previewWidth = int.Parse(WebConfigurationManager.AppSettings["ImageResizePreviewWidth"] ?? "0");
            int previewHeight = int.Parse(WebConfigurationManager.AppSettings["ImageResizePreviewHeight"] ?? "0");
            string previewFileNameSuffix = WebConfigurationManager.AppSettings["ImageResizePreviewSuffix"] ?? "";


            bool keepOriginal = bool.Parse(WebConfigurationManager.AppSettings["ImageResizeKeepOriginal"] ?? "false");
            bool upscale = bool.Parse(WebConfigurationManager.AppSettings["ImageResizeUpscale"] ?? "false");
            bool maintainRatio = bool.Parse(WebConfigurationManager.AppSettings["ImageResizeMaintainRatio"] ?? "false");
            bool applyToExistingImages = bool.Parse(WebConfigurationManager.AppSettings["ImageResizeApplyToExistingImages"] ?? "false");

            foreach (IMedia mediaItem in e.SavedEntities)
            {
                if (!string.IsNullOrEmpty(mediaItem.ContentType.Alias) && mediaItem.ContentType.Alias.ToLower() == "image" && (resizeWidth > 0 && resizeHeight > 0))
                {
                    bool isNew = mediaItem.Id <= 0;
                    if (isNew || applyToExistingImages)
                    {
                        string serverFilePath = GetServerFilePath(mediaItem, isNew, _logger);
                        if (serverFilePath != null)
                        {
                            var suppressFurtherSaves = HasSuppressFile(serverFilePath);
                            if (suppressFurtherSaves)
                            {
                                DeleteSuppressFile(serverFilePath);
                            }
                            else
                            {
                                double currentWidth = int.Parse(mediaItem.GetValue<string>("umbracoWidth"));
                                double currentHeight = int.Parse(mediaItem.GetValue<string>("umbracoHeight"));
                                Tuple<int, int> imageSize = GetCorrectWidthAndHeight(resizeWidth, resizeHeight, maintainRatio, currentWidth, currentHeight);
                                bool isDesiredSize = (currentWidth == imageSize.Item1) && (currentHeight == imageSize.Item2);
                                bool isLargeEnough = currentWidth >= imageSize.Item1 && currentHeight >= imageSize.Item2;

                                if (!isDesiredSize && (isLargeEnough || upscale))
                                {
                                    if (CreateCroppedVersionOfTheFile(imageSize.Item1, imageSize.Item2, fileNameSuffix, keepOriginal, serverFilePath))
                                    {
                                        CreateSuppressFile(serverFilePath);
                                        mediaItem.SetValue("umbracoWidth", imageSize.Item1);
                                        mediaItem.SetValue("umbracoHeight", imageSize.Item2);
                                        sender.Save(mediaItem);
                                    }
                                }

                                if (previewWidth > 0 && previewHeight > 0 && !string.IsNullOrWhiteSpace(previewFileNameSuffix))
                                {
                                    if (CreateCroppedVersionOfTheFile(previewWidth, previewHeight,
                                        previewFileNameSuffix, true, serverFilePath))
                                    {
                                        CreateSuppressFile(serverFilePath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the correct width and height depending on the desired size and whether the ratio is to be maintained or not.
        /// </summary>
        /// <param name="width">Desired width</param>
        /// <param name="height">Desired height</param>
        /// <param name="maintainRatio">Maintain ratio or not</param>
        /// <param name="currentWidth">Current width of the image</param>
        /// <param name="currentHeight">Current height of the image</param>
        /// <returns>The correct width and height for the new file</returns>
        private Tuple<int, int> GetCorrectWidthAndHeight(int width, int height, bool maintainRatio, double currentWidth, double currentHeight)
        {
            int newWidth = width;
            int newHeight = height;
            if (maintainRatio)
            {
                double widthToHeightRatio = currentWidth / currentHeight;
                bool isSquare = widthToHeightRatio == 1;
                bool isWider = widthToHeightRatio > 1;
                if (!isSquare && maintainRatio)
                {
                    if (isWider)
                    {
                        newWidth = (int)(newHeight * widthToHeightRatio);
                    }
                    else
                    {
                        newHeight = (int)(newWidth / widthToHeightRatio);
                    }

                }
            }
            return new Tuple<int, int>(newWidth, newHeight);
        }

        /// <summary>
        /// Gets the path of the file on the server
        /// </summary>
        /// <param name="mediaItem">The item to get the path from</param>
        /// <param name="isNew">Is this a new file or an existing one?</param>
        /// <returns>The path of the file on the server</returns>
        private static string GetServerFilePath(IMedia mediaItem, bool isNew, ILogger logger)
        {
            var filePath = mediaItem.GetUrl("umbracoFile", logger);
            if (filePath != null)
            {
                if (!filePath.StartsWith("/media/"))
                {
                    filePath = GetFilePathFromJson(filePath);
                }
                return HttpContext.Current.Server.MapPath(filePath);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the path of the existing file
        /// </summary>
        /// <param name="filePath">The json version of the file path</param>
        /// <returns>A string for the path of the file</returns>
        private static string GetFilePathFromJson(string filePath)
        {
            //var jsonFileDetails = JObject.Parse(filePath);
            //string src = jsonFileDetails["src"].ToString();
            //filePath = src;
            return filePath;
        }

        /// <summary>
        /// Creates a cropped version of the file using the given settings
        /// </summary>
        /// <param name="width">Desired width</param>
        /// <param name="height">Desired height</param>
        /// <param name="fileNameSuffix">Suffix of this cropped file</param>
        /// <param name="keepOriginal">Keep the original file or not, if yes, it will rename the new file to the original path after the original is deleted</param>
        /// <param name="originalFilePath">Original file path</param>
        /// <returns></returns>
        private bool CreateCroppedVersionOfTheFile(int width, int height, string fileNameSuffix, bool keepOriginal, string originalFilePath)
        {
            bool success = false;
            string newFilePath = GetNewFilePath(originalFilePath, fileNameSuffix);
            if (CreateCroppedImage(originalFilePath, newFilePath, width, height))
            {
                if (!keepOriginal)
                {
                    if (DeleteFile(originalFilePath))
                    {
                        success = RenameFile(newFilePath, originalFilePath);
                    }
                    else
                    {
                        success = true;
                    }
                }
                else
                {
                    success = true;
                }
            }
            return success;
        }

        /// <summary>
        /// This creates a text file called suppress.txt which indicates that we should not attempt
        /// to resize the image again to prevent an endless loop.
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <returns></returns>
        private bool CreateSuppressFile(string imageFilePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(imageFilePath);
                var folderPath = fileInfo.DirectoryName;
                var filePath = Path.Combine(folderPath, "suppress.txt");
                using (var sw = new StreamWriter(System.IO.File.Create(filePath)))
                {

                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// This method deletes the suppress.txt file in the folder of the file path provided.
        /// </summary>
        /// <param name="imageFilePath">The path of the image being resized</param>
        /// <returns></returns>
        private bool DeleteSuppressFile(string imageFilePath)
        {
            FileInfo fileInfo = new FileInfo(imageFilePath);
            var folderPath = fileInfo.DirectoryName;
            var filePath = Path.Combine(folderPath, "suppress.txt");
            return DeleteFile(filePath);
        }

        /// <summary>
        /// Checks if the suppress.txt file exists in the folder where the image is.
        /// </summary>
        /// <param name="imageFilePath"></param>
        /// <returns></returns>
        private bool HasSuppressFile(string imageFilePath)
        {
            FileInfo fileInfo = new FileInfo(imageFilePath);
            var folderPath = fileInfo.DirectoryName;
            var suppressFilePath = Path.Combine(folderPath, "suppress.txt");
            return System.IO.File.Exists(suppressFilePath);
        }

        /// <summary>
        /// Creates a cropped version of the image at the size specified in the parameters
        /// </summary>
        /// <param name="originalFilePath">The full path of the original file</param>
        /// <param name="newFilePath">The full path of the new file</param>
        /// <param name="width">The new image width</param>
        /// <param name="height">The new image height</param>
        /// <returns>A bool to show if the method was successful or not</returns>
        private bool CreateCroppedImage(string originalFilePath, string newFilePath, int width, int height)
        {
            bool success = false;
            ImageFactory imageFactory = new ImageFactory();
            try
            {
                imageFactory.Load(originalFilePath);
                ResizeLayer layer = new ResizeLayer(new Size(width, height), ResizeMode.Crop, AnchorPosition.Center);
                imageFactory.Resize(layer);
                imageFactory.AutoRotate();
                imageFactory.Save(newFilePath);
                success = true;
            }
            catch (System.Exception)
            {
                success = false;
            }
            finally
            {
                imageFactory.Dispose();
            }
            return success;
        }

        /// <summary>
        /// Creates a new file path using the original one and adding a suffix to the file name
        /// </summary>
        /// <param name="filePath">The full path of the original file</param>
        /// <param name="fileNameSuffix">The suffix to be used at the end of the file name in the new file path</param>
        /// <param name="folderPath">An out variable to return the folder path</param></para>
        /// <returns>The new file path</returns>
        public string GetNewFilePath(string filePath, string fileNameSuffix)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            string folderPath = fileInfo.DirectoryName;
            string fileExtension = fileInfo.Extension;
            string fullFileName = fileInfo.Name;
            string fileNameWithoutExtension = fullFileName.Substring(0, fullFileName.Length - fileExtension.Length);
            return string.Format("{0}\\{1}{2}{3}", folderPath, fileNameWithoutExtension, fileNameSuffix, fileExtension);
        }

        /// <summary>
        /// Deletes a file, if it exists
        /// </summary>
        /// <param name="filePath">The full path of the file to delete</param>
        /// <returns>A bool to show if the method was successful or not</returns>
        public bool DeleteFile(string filePath)
        {
            bool success = false;
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                success = true;
            }
            catch (System.Exception ex)
            {
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Renames a file by using the Move method.
        /// </summary>
        /// <param name="sourceFileName">The full path of the source file</param>
        /// <param name="destFileName">The full path of the destination file</param>
        /// <returns>A bool to show if the method was successful or not</returns>
        public bool RenameFile(string sourceFileName, string destFileName)
        {
            bool success = false;
            try
            {
                if (System.IO.File.Exists(sourceFileName) && !System.IO.File.Exists(destFileName))
                {
                    System.IO.File.Move(sourceFileName, destFileName);
                    success = true;
                }
            }
            catch (System.Exception)
            {
                success = false;
            }
            return success;
        }

        public void Terminate()
        {
            return;
        }
    }
}
