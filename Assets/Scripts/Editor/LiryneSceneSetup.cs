using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Liryne.Game.Editor
{
    /// <summary>Play démarre sur le boot. Titre et maps sont chargés ensuite.</summary>
    [InitializeOnLoad]
    public static class LiryneSceneSetup
    {
        const string BootPath = "Assets/Scenes/Liryne.unity";
        const string TitlePath = "Assets/Scenes/Title.unity";
        const string ClairierePath = "Assets/Scenes/Maps/Clairiere.unity";

        static LiryneSceneSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Liryne/Ouvrir Boot")]
        public static void OpenBoot() => Open(BootPath);

        [MenuItem("Liryne/Ouvrir Titre")]
        public static void OpenTitle() => Open(TitlePath);

        [MenuItem("Liryne/Ouvrir Clairière")]
        public static void OpenClairiere() => Open(ClairierePath);

        static void Open(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        static void Apply()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(TitlePath, true),
                new EditorBuildSettingsScene(ClairierePath, true),
            };

            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath);
            if (boot is not null)
            {
                EditorSceneManager.playModeStartScene = boot;
            }
        }
    }
}
