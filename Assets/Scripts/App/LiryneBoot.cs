using UnityEngine;

namespace Liryne.Game.App
{
    /// <summary>Démarre le client sans objet de scène : login, serveurs, personnages, map.</summary>
    public static class LiryneBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            LiryneApp existing = Object.FindAnyObjectByType<LiryneApp>();
            if (existing is not null)
            {
                if (existing.IsReady)
                {
                    return;
                }

                Object.DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject("Liryne");
            root.AddComponent<LiryneApp>();
        }
    }
}
