using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Place le village dans la scène, visible dans l’éditeur et en Play.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class VillageSceneAnchor : MonoBehaviour
    {
        void OnEnable()
        {
            Transform existing = transform.Find("VillageClairiere");
            if (existing is not null)
            {
                return;
            }

            VillageBuilder.Build(transform);
        }
    }
}
