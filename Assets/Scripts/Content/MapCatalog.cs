namespace Liryne.Game.Content
{
    public readonly struct MapInfo
    {
        public MapInfo(ushort id, string name, string scene)
        {
            Id = id;
            Name = name;
            Scene = scene;
        }

        public ushort Id { get; }
        public string Name { get; }
        public string Scene { get; }
    }

    /// <summary>Correspondance MapId serveur → scène Unity. Tenir aligné avec SliceMaps.</summary>
    public static class MapCatalog
    {
        public const ushort LyreGlade = 1;

        public static readonly MapInfo[] All =
        {
            new MapInfo(LyreGlade, "Clairière de la Lyre", "Clairiere"),
        };

        public static MapInfo Find(ushort mapId)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == mapId)
                {
                    return All[i];
                }
            }

            return default;
        }

        public static string Name(ushort mapId)
        {
            MapInfo info = Find(mapId);
            return string.IsNullOrEmpty(info.Name) ? "Terre " + mapId : info.Name;
        }

        public static string Scene(ushort mapId)
        {
            MapInfo info = Find(mapId);
            return info.Scene;
        }
    }
}
