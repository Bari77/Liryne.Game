# Liryne.Game

Client Unity. À lire avant d’écrire du code.

Ce dépôt est l’interface du jeu : scènes, personnages, animation, saisie, interface. Il envoie des intentions au serveur et joue ce que le serveur répond. Les chiffres de combat, de loot, de craft et de récolte viennent de `Liryne.Server`. Les types de messages viennent du package `Liryne.Protocol`.

## Dépôts

Trois dépôts git frères, ouverts ensemble par `C:\Users\Bari\Documents\Repo\Liryne.code-workspace`.

| Dépôt | Chemin | Rôle |
| --- | --- | --- |
| Liryne.Protocol | `../Liryne.Protocol` | Messages, sérialisation, protocole. Package NuGet `Liryne.Protocol`. |
| Liryne.Server | `../Liryne.Server` | Simulation, comptes, base de données. |
| Liryne.Game | ce dépôt | Client Unity. Affichage, saisie, animation. |

## Vision

Liryne (prononcé « li-rine ») est un MMORPG de combat en temps réel, au design manga. Deux jeux servent de référence de sensation, pas de contenu à recopier.

- NosTale pour le combat temps réel et les cartes SP. Équiper une SP change l’apparence, les compétences et la façon de jouer.
- Dofus pour les métiers de récolte, les métiers de craft, l’équipement et l’économie d’objets.

Les quatre affinités sont le feu, l’eau, la lumière et les ténèbres. Sur le logo, de gauche à droite, c’est cet ordre. Un même personnage change de gameplay en changeant d’affinité.

Le serveur est la source de vérité. Il simule le combat, les dégâts, le butin, l’inventaire, le craft et la récolte. Le client envoie des intentions (se déplacer, lancer le sort N sur la cible T) et joue le résultat qu’on lui renvoie.

La première cible jouable est une tranche verticale.

1. Un personnage et quatre SP, une par affinité, avec trois ou quatre sorts chacune.
2. Une map et une boucle de combat en temps réel.
3. Un métier de récolte, un métier de craft, une petite table d’objets et d’équipement.
4. Un partenaire et un familier, même simples.
5. Le style manga tenu dès cette tranche.

## Moteur

Unity 6, pipeline URP, style manga stylisé. Le réalisme photographique est hors sujet : il coûte des images par seconde sans servir le design. Le client prédit le déplacement local pour rester fluide, puis se recale sur l’état envoyé par World. La prédiction ne décide jamais d’un coup porté.

Le package `Liryne.Protocol` est consommé par NuGetForUnity (déclaré dans `Packages/manifest.json`) depuis le flux local du dépôt Protocol. La source est dans `Assets/NuGet.config` (`../../Liryne.Protocol/feed`), la version dans `Assets/packages.config`. Les paquets restaurés vont dans `Assets/Packages/`, ignoré par git. Pas de copie des classes de messages dans `Assets`.

Monter de version : produire le paquet dans Protocol (`dotnet pack src/Liryne.Protocol -c Release`), changer la version dans `Assets/packages.config`, puis **NuGet → Restore Packages** dans Unity. NuGetForUnity garde une copie de chaque paquet dans `%LocalAppData%\NuGet\Cache` : si une même version est republiée, supprimer `Liryne.Protocol.*` de ce cache et le dossier `Assets/Packages/Liryne.Protocol.<version>` avant de restaurer. Hors éditeur, `dnx NuGetForUnity.Cli restore .` fait la même restauration (variable `DOTNET_ROLL_FORWARD=Major` tant que la CLI cible .NET 9).

## Ce qui est déjà là

Deux images de logo, à la racine. Un projet Unity 6 (6000.6.10f1) en URP, issu du modèle, avec `Liryne.Protocol` 0.4.0 branché.

| Fichier | Contenu |
| --- | --- |
| `logo.png` | Lyre et le mot LIRYNE, fond indigo. Écran titre, communication. |
| `liryne-lyre.png` | Lyre seule, fond transparent, intérieur de la lyre transparent. Icône, HUD, curseur. |

La lyre a quatre cordes : feu, eau, lumière, ténèbres, de gauche à droite. L’étincelle au sommet marque le changement de SP. Ces fichiers sont la direction visuelle de départ. Les meshes, le rig et les animations de production viendront ensuite (Blender, Mixamo ou assets). Pour la tranche, des capsules et des animations de placeholder suffisent.

## État actuel

Client jouable. Scène par défaut : `Assets/Scenes/Liryne.unity` (Play la lance tout seul). Village de la Clairière autour du spawn : place, puits, auberge, maisons, marché, arbres. Login Auth, serveurs (`Assets/StreamingAssets/servers.json`), création / sélection / apparence, déplacement, chat, profil, inventaire vide. Placeholders capsules. Pas de sorts encore.

Dans Unity : ouvre le projet, appuie sur Play. Menu `Liryne → Ouvrir la scène du village` si une autre scène est ouverte. Auth + World doivent tourner (`http://127.0.0.1:8080`, TCP `7777`). WASD, Entrée chat, C profil, I sac.

## Premier travail

1. ~~Créer le projet Unity 6 en URP à la racine de ce dépôt, sans déplacer les deux logos hors du dépôt.~~
2. ~~Brancher `Liryne.Protocol` depuis le flux local.~~
3. ~~Une scène : un sol, un personnage placeholder, la lyre à l’écran titre.~~
4. ~~Brancher la connexion : session obtenue via Auth, puis entrée dans World.~~
5. Envoyer une intention de sort et jouer l’animation du résultat. Ne pas calculer les dégâts dans un script Unity.

## Garde-fous

- Aucun `MonoBehaviour` ne recalcule une formule de combat, de craft ou de récolte.
- Aucune classe de message dupliquée « le temps de tester ».
- Pas de stack réseau parallèle (Mirror, Netcode, Photon) qui réinventerait le protocole. Le transport du client porte les octets définis par `Liryne.Protocol`.
- Pas de contenu copié de NosTale ou de Dofus.
- Pas d’inventaire complet ni de boutique tant que le fil d’objets n’existe pas. Les sorts attendent encore leur HUD.
