using UnityEngine;

namespace Liryne.Game.World
{
    /// <summary>Données de fourreau / prise pour une arme procédurale.</summary>
    public sealed class WeaponGrip : MonoBehaviour
    {
        public Transform Sheath;
        public Transform Hand;
        public Vector3 SheathPos;
        public Quaternion SheathRot;
        public Vector3 HandPos;
        public Quaternion HandRot;
    }

    /// <summary>Passe les armes des fourreaux aux mains quand l'entité est en combat.</summary>
    public sealed class WeaponHold : MonoBehaviour
    {
        WeaponGrip[] _grips = System.Array.Empty<WeaponGrip>();
        bool _drawn;

        public void Bind()
        {
            _grips = GetComponentsInChildren<WeaponGrip>(true);
            _drawn = false;
            Apply(drawn: false);
        }

        void LateUpdate()
        {
            var avatar = GetComponentInParent<EntityAvatar>();
            bool drawn = avatar is not null && avatar.InCombat && !avatar.IsDead;
            if (drawn == _drawn)
            {
                return;
            }

            _drawn = drawn;
            Apply(drawn);
        }

        void Apply(bool drawn)
        {
            for (int i = 0; i < _grips.Length; i++)
            {
                WeaponGrip grip = _grips[i];
                if (grip is null)
                {
                    continue;
                }

                Transform parent = drawn && grip.Hand is not null ? grip.Hand : grip.Sheath;
                if (parent is null)
                {
                    continue;
                }

                grip.transform.SetParent(parent, false);
                grip.transform.localPosition = drawn ? grip.HandPos : grip.SheathPos;
                grip.transform.localRotation = drawn ? grip.HandRot : grip.SheathRot;
            }
        }
    }
}
