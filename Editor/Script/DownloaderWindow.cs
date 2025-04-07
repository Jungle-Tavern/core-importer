using UnityEditor;
using UnityEngine;

namespace JT
{
    public class DownloaderWindow : EditorWindow
    {
        private string _githubToken;
        
        private static ProjectDownloader _projectDownloader;

        private static ProjectDownloader ProjectDownloader
        {
            get => _projectDownloader ??= new ProjectDownloader();
        }
        
        private void OnEnable()
        {
            TryLoadToken();

            _projectDownloader = new ProjectDownloader();
    
            if (SessionState.GetBool(DownloadingConstants.IsInstallingPackagesKey, false))
            {
                Debug.Log("Resuming interrupted package install...");
                _projectDownloader.ResumeInstallingPackages(_githubToken);
            }
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
                ProjectDownloader.SetupPackages(_githubToken);
            }
        }

        private void TryLoadToken()
        {
            _githubToken = TokenPrefsHelper.Load();
        }
        
        private static void ShowProjectDownloaderWindow()
        {
            DownloaderWindow window =
                (DownloaderWindow)GetWindow(typeof(DownloaderWindow));
            window.titleContent.text = "Core Importer";
            window.Show();
        }
        
        [MenuItem("Tools/Core Importer")]
        private static void ShowProjectSettingsTuner()
        {
            ShowProjectDownloaderWindow();
        }
    }
}