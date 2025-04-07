using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.SharpZipLib.Core;
using Unity.SharpZipLib.Zip;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace JT
{
    public class ProjectDownloader
    {
        private static readonly string[] PackagePaths =
        {
            "com.unity.nuget.newtonsoft-json",
            "https://github.com/neuecc/UniRx.git?path=Assets/Plugins/UniRx/Scripts",
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.16.8",
        };

        private EditorCoroutine _coroutine;

        public void SetupPackages(string token)
        {
            if (_coroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(_coroutine);
                _coroutine = null;
            }

            int startIndex = SessionState.GetBool(DownloadingConstants.IsInstallingPackagesKey, false)
                ? SessionState.GetInt(DownloadingConstants.PackageIndexKey, 0)
                : 0;

            _coroutine = EditorCoroutineUtility.StartCoroutineOwnerless(PackageInstallerCor(token, startIndex));
        }
        
        public void ResumeInstallingPackages(string token)
        {
            if (!SessionState.GetBool(DownloadingConstants.IsInstallingPackagesKey, false)) return;

            int index = SessionState.GetInt(DownloadingConstants.PackageIndexKey, 0);
            _coroutine = EditorCoroutineUtility.StartCoroutineOwnerless(PackageInstallerCor(token, index));
        }
        
        private IEnumerator PackageInstallerCor(string token, int startIndex)
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted) yield return null;

            var installedPackages = listRequest.Result;

            SessionState.SetBool(DownloadingConstants.IsInstallingPackagesKey, true);

            for (int i = startIndex; i < PackagePaths.Length; i++)
            {
                var package = PackagePaths[i];

                if (installedPackages.Any(p => package.Contains(p.name))) continue;

                Debug.Log($"Installing: {package}");
                SessionState.SetInt(DownloadingConstants.PackageIndexKey, i);

                yield return PackagesImportingCor(package);
            }

            EditorUtility.ClearProgressBar();
            Debug.Log("All packages installed.");
            
            DownloadProject(token);
            
            SessionState.EraseInt(DownloadingConstants.PackageIndexKey);
            SessionState.SetBool(DownloadingConstants.IsInstallingPackagesKey, false);
        }

        private IEnumerator PackagesImportingCor(string package)
        {
            var request = Client.Add(package);
            EditorUtility.DisplayProgressBar("Importing", $"Importing package: {package}", 0);

            float startTime = Time.realtimeSinceStartup;
            const float timeout = 30f;

            yield return new WaitUntil(() =>
            {
                bool isStuck = (Time.realtimeSinceStartup - startTime) > timeout;
                if (isStuck)
                {
                    Debug.LogError($"Package installation timeout: {package}");
                    return true;
                }

                return request.IsCompleted;
            });

            EditorUtility.ClearProgressBar();

            if (request.Status == StatusCode.Failure)
            {
                Debug.LogError($"Failed to import package: {package}\n{request.Error?.message}");
            }
        }
        
        private static void SaveByteArrayToFileWithFileStream(byte[] data, string filePath)
        {
            using var stream = File.Create(filePath);
            stream.Write(data, 0, data.Length);
        }
        
        private static void UncompressFromZip(string archivePath, string relativePath, string outFolder)
        {
            using var fs = File.OpenRead(archivePath);
            using var zf = new ZipFile(fs);

            foreach (ZipEntry zipEntry in zf)
            {
                if (!zipEntry.IsFile)
                {
                    continue;
                }

                var fileDirectory = Path.GetDirectoryName(zipEntry.Name);
                if (fileDirectory != null)
                {
                    var s = fileDirectory.Split('/');
                    string cached = string.Empty;
                    for (int i = 0; i < s.Length; i++)
                    {
                        if (s[i].StartsWith(relativePath))
                        {
                            cached = s[i];
                        }
                    }
                    
                    var entryFileName = zipEntry.Name.Remove(0, cached.Length == 0 ? 0 : cached.Length + 1);
                    var fullZipToPath = Path.Combine(outFolder, entryFileName);
                    var directoryName = Path.GetDirectoryName(fullZipToPath);

                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                
                    var buffer = new byte[4096];
                
                    using var zipStream = zf.GetInputStream(zipEntry);
                    using Stream fsOutput = File.Create(fullZipToPath);
                
                    StreamUtils.Copy(zipStream, fsOutput, buffer);
                }
            }
        }
        
        private void DownloadProject(string token)
        {
            var url = "https://github.com/Jungle-Tavern/core/zipball/master/";
            var pathToFolder = Path.Combine(Application.dataPath, "Meta");

            using (var client = new System.Net.Http.HttpClient())
            {
                var credentials =
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:", token);
                credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(credentials));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                var contents = client.GetByteArrayAsync(url).Result;
                EditorUtility.DisplayProgressBar("Download", "Download Repository zip", 0);
                try
                {
                    var pathToFile = Path.Combine(Application.dataPath, "RepositoryArchive");

                    SaveByteArrayToFileWithFileStream(contents, pathToFile);
                    
                    UncompressFromZip(pathToFile, "Jungle-Tavern-core", pathToFolder);
                    FileUtil.DeleteFileOrDirectory(pathToFile);
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                }
                catch (Exception e)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogError(e);
                }
                
            }
            
            EditorUtility.ClearProgressBar();
        }
    }
}