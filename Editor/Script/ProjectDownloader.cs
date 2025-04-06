using System;
using System.Collections;
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace JT
{
    public class ProjectDownloader : EditorWindow
    {
        private static readonly string[] PackagePaths =
        {
            "com.unity.nuget.newtonsoft-json",
            "https://github.com/neuecc/UniRx.git?path=Assets/Plugins/UniRx/Scripts",
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.16.8",
        };

        private string _githubToken;
        
        private EditorCoroutine _coroutine;

        private void OnEnable()
        {
            TryLoadToken();
        }
        
        private void TryLoadToken()
        {
            _githubToken = TokenPrefsHelper.Load();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            _githubToken = EditorGUILayout.TextField("Token", _githubToken);
            
            if (GUILayout.Button("Save Github Token"))
            {
                TokenPrefsHelper.Save(_githubToken);
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button($"Import Core Packages"))
            {
                SetupPackages();
            }
        }

        [MenuItem("Tools/Core Importer")]
        private static void ShowProjectSettingsTuner()
        {
            ShowProjectDownloaderWindow();
        }

        private static void ShowProjectDownloaderWindow()
        {
            ProjectDownloader window =
                (ProjectDownloader)GetWindow(typeof(ProjectDownloader));
            window.titleContent.text = "Core Importer";
            window.Show();
        }
        
        private void SetupPackages()
        {
            if (_coroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(_coroutine);
                _coroutine = null;
            }
            
            _coroutine = EditorCoroutineUtility.StartCoroutine(Cor(), this);
        }

        private IEnumerator Cor()
        {
            foreach (var package in PackagePaths)
            {
                var request = Client.Add(package);
                EditorUtility.DisplayProgressBar("Importing", "Importing packages", 0);
                yield return new WaitUntil(() => request.IsCompleted);
                EditorUtility.ClearProgressBar();
            }
            
            DownloadProject();
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
        
        private void DownloadProject()
        {
            var url = "https://github.com/Jungle-Tavern/core/zipball/master/";
            var pathToFolder = Path.Combine(Application.dataPath, "Meta");

            using (var client = new System.Net.Http.HttpClient())
            {
                var credentials =
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:", _githubToken);
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