using System;
using System.Threading.Tasks;
using Liryne.Game.Content;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Liryne.Game.App
{
    /// <summary>Charge le titre ou la map du personnage, sans empiler le village derrière le login.</summary>
    public sealed class SceneFlow
    {
        public const string TitleScene = "Title";

        public string LoadedMap { get; private set; }

        public bool TitleIsReady =>
            SceneManager.GetSceneByName(TitleScene).isLoaded && string.IsNullOrEmpty(LoadedMap);

        public async Task LoadTitleAsync()
        {
            await UnloadMapAsync();
            await EnsureLoadedAsync(TitleScene);
        }

        public async Task LoadMapAsync(ushort mapId)
        {
            string scene = MapCatalog.Scene(mapId);
            if (string.IsNullOrEmpty(scene))
            {
                throw new InvalidOperationException("Aucune scène client pour la map " + mapId + ".");
            }

            await UnloadAsync(TitleScene);
            if (LoadedMap == scene && SceneManager.GetSceneByName(scene).isLoaded)
            {
                return;
            }

            await UnloadMapAsync();
            await EnsureLoadedAsync(scene);
            LoadedMap = scene;
        }

        public async Task UnloadMapAsync()
        {
            if (string.IsNullOrEmpty(LoadedMap))
            {
                return;
            }

            await UnloadAsync(LoadedMap);
            LoadedMap = null;
        }

        static async Task EnsureLoadedAsync(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                return;
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op is null)
            {
                throw new InvalidOperationException(
                    "Scène introuvable dans le Build Settings : " + sceneName + ".");
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }
        }

        static async Task UnloadAsync(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            if (op is null)
            {
                return;
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }
        }
    }
}
