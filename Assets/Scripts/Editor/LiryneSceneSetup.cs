using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Liryne.Game.Editor
{
    /// <summary>Play lance toujours la scène du village, sans réglage à faire.</summary>
    [InitializeOnLoad]
    public static class LiryneSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Liryne.unity";

        static LiryneSceneSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Liryne/Ouvrir la scène du village")]
        public static void OpenVillageScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        static void Apply()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (scene is null)
            {
                return;
            }

            EditorSceneManager.playModeStartScene = scene;
        }
    }
}
