using System;
using UnityEngine;

namespace Liryne.Game.Ui
{
    [Serializable]
    sealed class HudPanelPose
    {
        public string Id;
        public float X;
        public float Y;
    }

    [Serializable]
    sealed class HudLayoutData
    {
        public HudPanelPose[] Panels = Array.Empty<HudPanelPose>();
        public int[] Bar = new int[HudView.SpellSlots];
    }

    /// <summary>Positions HUD et barre de sorts, enregistrées après un court délai d'inactivité.</summary>
    sealed class HudLayoutStore
    {
        const string PrefsKey = "liryne.hud.layout.v1";
        const float DebounceSeconds = 0.8f;

        readonly HudLayoutData _data;
        float _dirtyAt = -1f;

        public HudLayoutStore()
        {
            _data = Load();
        }

        public HudLayoutData Data => _data;

        public void MarkDirty() => _dirtyAt = Time.unscaledTime;

        public void Tick()
        {
            if (_dirtyAt < 0f || Time.unscaledTime - _dirtyAt < DebounceSeconds)
            {
                return;
            }

            Save();
        }

        public void Flush()
        {
            if (_dirtyAt >= 0f)
            {
                Save();
            }
        }

        void Save()
        {
            _dirtyAt = -1f;
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }

        static HudLayoutData Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return Fresh();
            }

            try
            {
                HudLayoutData data = JsonUtility.FromJson<HudLayoutData>(json);
                if (data is null)
                {
                    return Fresh();
                }

                if (data.Bar is null || data.Bar.Length != HudView.SpellSlots)
                {
                    var bar = new int[HudView.SpellSlots];
                    if (data.Bar is not null)
                    {
                        Array.Copy(data.Bar, bar, Math.Min(data.Bar.Length, bar.Length));
                    }

                    data.Bar = bar;
                }

                data.Panels ??= Array.Empty<HudPanelPose>();
                return data;
            }
            catch
            {
                return Fresh();
            }
        }

        static HudLayoutData Fresh() => new HudLayoutData
        {
            Panels = Array.Empty<HudPanelPose>(),
            Bar = new int[HudView.SpellSlots],
        };
    }
}
