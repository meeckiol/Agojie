# 🗡️ Agojie — Action / Infiltration (Royaume du Dahomey)

Jeu 3D d'action et d'infiltration inspiré d'Assassin's Creed et God of War, se déroulant à Abomey au XIXe siècle.

## 🛠️ Stack Technique
* **Moteur** : Unity LTS (recommandé : Unity 2022.3 LTS ou Unity 6 LTS)
* **Langage** : C# (.NET)
* **Système de navigation** : Unity NavMesh & AI Navigation
* **Contrôle de version** : Git & Git LFS

---

## 👥 Répartition des Équipes & Branches Git

| Développeur | Domaine | Modules clés |
| :--- | :--- | :--- |
| **Toi** | **IA, Quêtes & UI** | `EnemyController`, `EnemyPerception`, `QuestManager`, `DialogueManager`, `HealthBarUI`, `CompassUI`, `PauseMenuUI` |
| **Ton binôme** | **Joueur & Monde** | `PlayerController`, Camera 3rd person, Combat joueur (combos/esquive), Level Design |

### Recommandation Branches Git :
* `main` : Version stable et testée.
* `feature/enemy-systems` (Ta branche de travail) :
  ```bash
  git checkout -b feature/enemy-systems
  ```
* `feature/player-gameplay` (Branche de ton binôme)

---

## 📁 Architecture des Scripts (`Assets/Scripts/`)

* **`Core/`** :
  * `IDamageable.cs` : Interface universelle pour tout ce qui subit des dégâts.
  * `IInteractable.cs` : Interface pour les PNJ, leviers, objets de quête.
  * `HealthComponent.cs` : Composant santé réutilisable (Joueur, Ennemis).
* **`Player/`** :
  * `PlayerMovement.cs` : Marcher, courir (bruyant), s'accroupir (discret), esquive avec invulnérabilité, saut, escalade (layer `Climbable`).
  * `ThirdPersonCamera.cs` : Caméra 3e personne à la souris, anti-collision murs, tremblement à l'impact. Échap est géré par `PauseMenuUI`.
  * `PlayerCombat.cs` : Combo léger 3 coups, attaque lourde qui étourdit, assassinat furtif, micro-pause à l'impact. Frappe via `IDamageable`.
  * `PlayerInteractor.cs` : Parler aux PNJ / utiliser les `IInteractable` (touche E).
  * `PlayerDeathHandler.cs` : Message de mort puis redémarrage du niveau.
  * `PlayerMockTarget.cs` : Cible de test pour l'IA. **Ne pas** la mettre sur le vrai joueur (elle force le layer `Default`).
* **`Visuals/`** :
  * `ProceduralAnimator.cs` : Anime les personnages sans fichier d'animation (marche, course, accroupi, escalade, combos, roulade, étourdi, coup reçu, mort) et joue les bruits de pas.
* **`Audio/`** :
  * `GameAudio.cs` : `AudioManager` (sons 3D + musique) et le raccourci `GameAudio.Play(Sfx.Hit, position)`.
  * `ProceduralSounds.cs` : Tous les sons sont calculés par le code (aucun fichier). Musique : cloche gankogui et tambours en 12/8.
* **`World/`** :
  * `RuntimeNavMeshBaker.cs` : Construit le NavMesh au lancement (aucun package requis).
* **`Editor/`** :
  * `AgojieTestLevelBuilder.cs` : Menu **Agojie → Construire la scène de test** (niveau jouable en un clic).
* **`AI/`** :
  * `EnemyState.cs` : Machine à états (Patrol, Suspicious, Chase, Attack, Stunned, Dead).
  * `EnemyPerception.cs` : Cône de vision 3D (FOV), détection d'infiltration (gauge jaune/rouge), ouïe.
  * `EnemyController.cs` : Déplacement NavMesh, enchaînement des patrouilles, attaques et combos.
* **`Quests/`** :
  * `QuestData.cs` (ScriptableObject) : Définition d'une mission et de ses objectifs.
  * `QuestManager.cs` : Gestionnaire de missions actives et progression.
  * `DialogueData.cs` (ScriptableObject) : Arbre de dialogue interactif et choix.
  * `DialogueManager.cs` : Moteur de dialogue avec effet machine à écrire.
  * `NPCDialogue.cs` : PNJ avec qui on parle (E).
  * `QuestInteractable.cs` : Objet/personne de mission (lettre à ramasser, captif à libérer).
  * `QuestZone.cs` : Zone à atteindre (objectif InfiltrateZone).
  * `QuestData.nextQuestId` : enchaîne automatiquement la mission suivante.
* **`UI/`** :
  * `DialogueUI.cs` : Boîte de dialogue, effet machine à écrire, choix cliquables.
  * `FaceCamera.cs` : Garde les jauges au-dessus des ennemis tournées vers la caméra.
  * `HealthBarUI.cs` : Barre de vie fluide avec effet "ghost bar" après dégâts.
  * `StealthDetectionUI.cs` : Indicateur d'alerte / détection au-dessus de l'ennemi.
  * `CompassUI.cs` : Barre de boussole 360° en haut d'écran.
  * `QuestTrackerUI.cs` : Affichage de l'objectif de mission en temps réel.
  * `PauseMenuUI.cs` : Menu pause complet avec reprise, options et arrêt du temps.

---

## 🚀 Installation de Unity Hub sur Ubuntu

Pour installer officiellement Unity Hub sur Ubuntu via le terminal :

```bash
# 1. Ajouter la clé publique Unity
sudo install -d /etc/apt/keyrings
curl -fsSL https://hub.unity3d.com/linux/keys/public | sudo gpg --dearmor -o /etc/apt/keyrings/unityhub.gpg

# 2. Ajouter le dépôt Unity Hub
sudo tee /etc/apt/sources.list.d/unityhub.sources > /dev/null << EOF
Types: deb
URIs: https://hub.unity3d.com/linux/repos/deb
Suites: stable
Components: main
Signed-By: /etc/apt/keyrings/unityhub.gpg
EOF

# 3. Mettre à jour et installer
sudo apt update
sudo apt install unityhub
```

Ensuite, lancez `unityhub`, connectez votre compte Unity (gratuit), et installez la version **Unity LTS** recommandée avec votre binôme.

---

## 🎮 Jouer tout de suite

1. **Edit → Project Settings → Player → Active Input Handling** : `Both` (Unity redémarre).
2. Menu **Agojie → Construire la scène de test**. Ça crée les layers `Player`, `Enemy`, `Climbable`, le niveau, la mission et le HUD, puis enregistre `Assets/Agojie_Test/AgojieTest.unity`.
3. **Play** ▶. Parle à la Hounon (E) et accepte la mission.

### Les 3 missions
1. **Le capitaine du marché** : élimine le capitaine négrier (assassinat furtif conseillé).
2. **Les lettres du traître** : trouve 3 lettres (une en haut de la tour de guet : il faut grimper).
3. **L'entrepôt des chaînes** : infiltre l'entrepôt au nord-est et libère les 3 captifs.

### Ce qui est généré par le constructeur
* Personnages 3D animés (Agojie, gardes, capitaine, Hounon, captifs) faits de formes simples.
* Décor : maisons en banco à toit de chaume, cases rondes, étals, palmiers, jarres, remparts, entrepôt éclairé.
* Ambiance : soleil de fin de journée, brume de latérite, musique et bruitages.
* Pour de vrais modèles plus tard : remplacer l'enfant `Modele` d'un personnage par un modèle Mixamo et brancher son `Animator` (les scripts envoient déjà les triggers `Attack1`, `HeavyAttack`, `Dodge`, etc.).

| Touche | Action |
| :--- | :--- |
| ZQSD | Se déplacer |
| Maj | Courir (fait du bruit : les gardes viennent voir) |
| C | S'accroupir (vu de moins loin, caché derrière les murets) |
| Ctrl gauche | Esquive (invulnérable un court instant) |
| Espace | Sauter / lâcher un mur |
| Clic gauche | Combo 3 coups |
| Clic droit | Attaque lourde (étourdit) |
| F | Assassinat furtif (ennemi qui ne t'a pas repérée) |
| E | Parler / interagir / faire avancer un dialogue |
| Échap | Pause |

La jauge au-dessus d'un garde passe du jaune (suspicion) au rouge (repérée).

## 🐞 Corrections apportées
* Détection : la jauge se remplissait en moins d'une seconde (furtivité impossible) ; `OnTargetSpotted` était envoyé à chaque frame.
* Un ennemi mort ou étourdi pouvait encore porter son coup ; les appels NavMesh plantaient hors NavMesh ; un ennemi n'entendait pas les bruits.
* Les quêtes restaient « Terminées » d'une partie à l'autre dans l'éditeur (ScriptableObject non réinitialisé).
* `QuestTrackerUI` pouvait ne jamais s'abonner (ordre Awake/OnEnable) ; la barre fantôme de `HealthBarUI` restait basse après un soin.
* `DialogueManager` n'avait ni interface ni effet machine à écrire : ajout de `DialogueUI`.
