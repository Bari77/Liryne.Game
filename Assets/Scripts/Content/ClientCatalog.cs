using System;
using Liryne.Protocol.Messages;
using Liryne.Protocol.Messages.Auth;
using Liryne.Protocol.Messages.Characters;
using Liryne.Protocol.Messages.Chat;
using Liryne.Protocol.Messages.Session;
using UnityEngine;

namespace Liryne.Game.Content
{
    /// <summary>
    /// Noms et palettes d'affichage. Les identifiants suivent le contenu World ;
    /// aucune règle de combat ici.
    /// </summary>
    public static class ClientCatalog
    {
        public const byte BodyTypes = 2;
        public const byte Faces = 8;
        public const byte HairStyles = 12;
        public const byte HairColors = 16;
        public const byte SkinTones = 8;
        public const byte EyeColors = 10;

        public static readonly ClassInfo[] Classes =
        {
            new ClassInfo(1, "Gardien", "Lame et bouclier"),
            new ClassInfo(2, "Duelliste", "Deux lames courtes"),
            new ClassInfo(3, "Traqueur", "Arc"),
            new ClassInfo(4, "Arcaniste", "Sceptre et grimoire"),
            new ClassInfo(5, "Clerc", "Masse et symbole sacré"),
            new ClassInfo(6, "Invocateur", "Catalyseur et sceaux"),
        };

        static readonly Color[] SkinPalette =
        {
            Hex("f3d1b3"), Hex("e4b48a"), Hex("d09a6a"), Hex("b97a4a"),
            Hex("8d5a38"), Hex("6b3f28"), Hex("4a2a1c"), Hex("2e1a12"),
        };

        public readonly struct ClassInfo
        {
            public ClassInfo(ushort id, string name, string blurb)
            {
                Id = id;
                Name = name;
                Blurb = blurb;
            }

            public ushort Id { get; }
            public string Name { get; }
            public string Blurb { get; }
        }

        public static string ClassName(ushort classId)
        {
            for (int i = 0; i < Classes.Length; i++)
            {
                if (Classes[i].Id == classId)
                {
                    return Classes[i].Name;
                }
            }

            return "Classe " + classId;
        }

        public static string TitleName(ushort titleId) => titleId switch
        {
            0 => "—",
            1 => "Voyageur de la Lyre",
            _ => "Titre " + titleId,
        };

        public static string OrnamentName(ushort ornamentId) => ornamentId switch
        {
            0 => "—",
            1 => "fil-aube",
            _ => "Ornement " + ornamentId,
        };

        public static string AffinityName(Affinity affinity) => affinity switch
        {
            Affinity.Fire => "Feu",
            Affinity.Water => "Eau",
            Affinity.Light => "Lumière",
            Affinity.Darkness => "Ténèbres",
            _ => affinity.ToString(),
        };

        public static Color SkinColor(byte id) => Pick(SkinPalette, id);

        public static Color HairColor(byte id)
        {
            Color[] hair =
            {
                Hex("1a1210"), Hex("3b2416"), Hex("6a3a1c"), Hex("c4a45a"),
                Hex("efe6c8"), Hex("f2f0ea"), Hex("d45a8a"), Hex("7a5ad4"),
                Hex("3a6ad4"), Hex("2a9a8a"), Hex("c43a3a"), Hex("8a3a6a"),
                Hex("4a8a3a"), Hex("d48a3a"), Hex("5a5a6a"), Hex("2a2a3a"),
            };
            return Pick(hair, id);
        }

        public static Color EyeColor(byte id)
        {
            Color[] eyes =
            {
                Hex("4a3018"), Hex("c48a2a"), Hex("3a8a4a"), Hex("3a6ad4"),
                Hex("7a7a8a"), Hex("7a4ad4"), Hex("d4b43a"), Hex("2a9aaa"),
                Hex("c43a4a"), Hex("1a1a1a"),
            };
            return Pick(eyes, id);
        }

        public static string BodyLabel(byte id) => id == 0 ? "Adolescent" : "Jeune adulte";

        public static string FaceLabel(byte id) => "Visage " + (id + 1);

        public static string HairStyleLabel(byte id) => "Coupe " + (id + 1);

        public static CharacterAppearance Clamp(CharacterAppearance appearance) =>
            new CharacterAppearance(
                (byte)(appearance.BodyType % BodyTypes),
                (byte)(appearance.FaceId % Faces),
                (byte)(appearance.HairStyleId % HairStyles),
                (byte)(appearance.HairColorId % HairColors),
                (byte)(appearance.SkinToneId % SkinTones),
                (byte)(appearance.EyeColorId % EyeColors));

        public static string AuthStatusText(AuthStatus status) => status switch
        {
            AuthStatus.Ok => string.Empty,
            AuthStatus.InvalidCredentials => "Identifiant ou mot de passe incorrect.",
            AuthStatus.LoginTaken => "Cet identifiant est déjà pris.",
            AuthStatus.InvalidInput => "Identifiant ou mot de passe invalide.",
            AuthStatus.ServerError => "Le service de comptes a un souci. Réessaie.",
            AuthStatus.Banned => "Ce compte est banni.",
            _ => "Connexion refusée.",
        };

        public static string CreateStatusText(CreateCharacterStatus status) => status switch
        {
            CreateCharacterStatus.Ok => string.Empty,
            CreateCharacterStatus.NameTaken => "Ce nom est déjà pris.",
            CreateCharacterStatus.NameInvalid => "Nom : 3 à 16 lettres, un tiret possible.",
            CreateCharacterStatus.ClassInvalid => "Cette classe n'est pas proposée.",
            CreateCharacterStatus.AppearanceInvalid => "Apparence hors catalogue.",
            CreateCharacterStatus.SlotLimit => "Tu as déjà 20 personnages sur ce compte.",
            CreateCharacterStatus.AlreadyInWorld => "Tu es déjà en jeu.",
            _ => "Création refusée.",
        };

        public static string AppearanceStatusText(ChangeAppearanceStatus status) => status switch
        {
            ChangeAppearanceStatus.Ok => string.Empty,
            ChangeAppearanceStatus.CharacterNotFound => "Personnage introuvable.",
            ChangeAppearanceStatus.InvalidAppearance => "Apparence hors catalogue.",
            ChangeAppearanceStatus.InWorld => "Change l'apparence depuis la sélection.",
            _ => "Apparence refusée.",
        };

        public static string WelcomeStatusText(WorldStatus status) => status switch
        {
            WorldStatus.Ok => string.Empty,
            WorldStatus.InvalidSession => "Session expirée. Reconnecte-toi.",
            WorldStatus.ProtocolMismatch => "Client trop ancien. Mets Liryne à jour.",
            WorldStatus.ServerFull => "Ce serveur est plein.",
            _ => "World a refusé la connexion.",
        };

        public static string DisconnectText(DisconnectReason reason) => reason switch
        {
            DisconnectReason.ServerShutdown => "Le serveur s'arrête.",
            DisconnectReason.Kicked => "Tu as été expulsé.",
            DisconnectReason.DuplicateLogin => "Connexion ouverte ailleurs.",
            DisconnectReason.ProtocolError => "Erreur de protocole.",
            DisconnectReason.Timeout => "Connexion trop longue sans activité.",
            DisconnectReason.Banned => "Compte banni.",
            _ => "Déconnecté.",
        };

        public static string ChatRejectText(ChatRejectReason reason) => reason switch
        {
            ChatRejectReason.Cooldown => "Trop vite sur ce canal. Attends un instant.",
            ChatRejectReason.Muted => "Tu es réduit au silence.",
            ChatRejectReason.NotAllowed => "Canal interdit pour toi.",
            ChatRejectReason.Invalid => "Message invalide.",
            _ => "Message refusé.",
        };

        public static string ChannelLabel(ChatChannel channel) => channel switch
        {
            ChatChannel.System => "Système",
            ChatChannel.Say => "Dire",
            ChatChannel.Map => "Map",
            ChatChannel.Trade => "Échange",
            ChatChannel.Recruitment => "Recrut",
            ChatChannel.Whisper => "Murmure",
            ChatChannel.Party => "Groupe",
            ChatChannel.Guild => "Guilde",
            ChatChannel.Staff => "Staff",
            _ => channel.ToString(),
        };

        public static Color ChannelColor(ChatChannel channel) => channel switch
        {
            ChatChannel.System => Hex("c4b48a"),
            ChatChannel.Say => Hex("eee8f8"),
            ChatChannel.Map => Hex("8ad4f0"),
            ChatChannel.Trade => Hex("f0d48a"),
            ChatChannel.Recruitment => Hex("8af0b4"),
            ChatChannel.Whisper => Hex("e0a0f0"),
            ChatChannel.Staff => Hex("f08a8a"),
            _ => Color.white,
        };

        static Color Pick(Color[] palette, byte id) => palette[id % palette.Length];

        static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color))
            {
                return Color.magenta;
            }

            return color;
        }
    }
}
