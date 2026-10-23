using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Agojie.AI;
using Agojie.Audio;
using Agojie.Core;
using Agojie.Player;
using Agojie.Quests;
using Agojie.UI;
using Agojie.Visuals;
using Agojie.World;

namespace Agojie.EditorTools
{
    /// <summary>
    /// Menu "Agojie > Construire la scène de test" : crée en un clic un niveau jouable.
    /// Marché d'Abomey (maisons, cases, étals, palmiers), entrepôt des captifs,
    /// personnages 3D animés, sons, 3 missions qui s'enchaînent, HUD.
    /// </summary>
    public static class AgojieTestLevelBuilder
    {
        const string Root = "Assets/Agojie_Test";
        const string ScenePath = Root + "/AgojieTest.unity";
        const int IgnoreRaycastLayer = 2;

        static int playerLayer;
        static int enemyLayer;
        static int climbLayer;
        static Font font;
        static Sprite uiSprite;

        [MenuItem("Agojie/Construire la scène de test")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            playerLayer = EnsureLayer("Player");
            enemyLayer = EnsureLayer("Enemy");
            climbLayer = EnsureLayer("Climbable");
            Directory.CreateDirectory(Root + "/Data");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh();

            font = LoadFont();
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera cam = Object.FindFirstObjectByType<Camera>();

            List<QuestData> quests = CreateQuests();
            DialogueData dialogue = CreateDialogue();

            SetupLighting(cam);
            BuildLevel();
            BuildWarehouse();
            GameObject player = BuildPlayer(cam.transform);
            BuildCamera(cam, player.transform);
            BuildEnemies();
            BuildNpc(dialogue);
            BuildLetters();
            BuildManagers(quests);
            BuildHud(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[Agojie] Scène de test créée (" + ScenePath + "). Appuie sur Play !");
        }

        // ---------------------------------------------------------------- Ambiance
        static void SetupLighting(Camera cam)
        {
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
                sun.color = new Color(1f, 0.88f, 0.72f);   // soleil de fin d'après-midi
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
            }
            Color haze = new Color(0.9f, 0.76f, 0.58f);   // poussière de latérite
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.47f, 0.4f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = haze;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 95f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = haze;
        }

        // ---------------------------------------------------------------- Niveau
        static void BuildLevel()
        {
            GameObject level = new GameObject("Niveau_Marche_Abomey");
            RuntimeNavMeshBaker baker = level.AddComponent<RuntimeNavMeshBaker>();
            SetMask(baker, "includedLayers", ~((1 << playerLayer) | (1 << enemyLayer) | (1 << IgnoreRaycastLayer)));

            Transform t = level.transform;
            Material ground = Mat("Terre", new Color(0.72f, 0.45f, 0.25f));
            Material path = Mat("Chemin", new Color(0.6f, 0.38f, 0.22f));
            Material banco = Mat("Banco", new Color(0.58f, 0.3f, 0.18f));
            Material wood = Mat("Bois", new Color(0.36f, 0.24f, 0.13f));

            Block("Sol", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 70f), ground, 0, t);
            Visual(PrimitiveType.Cube, "Chemin_Marche", t, new Vector3(0f, 0.01f, -6f), new Vector3(4f, 0.02f, 34f), path);
            Visual(PrimitiveType.Cube, "Chemin_Entrepot", t, new Vector3(12f, 0.01f, 24f), new Vector3(20f, 0.02f, 3f), path);

            // Maisons en banco avec toit de chaume : on se cache derrière.
            House("Maison_1", new Vector3(-8f, 0f, 6f), new Vector3(5f, 3f, 5f), banco, t);
            House("Maison_2", new Vector3(8f, 0f, 6f), new Vector3(5f, 3f, 5f), banco, t);
            House("Maison_3", new Vector3(-9f, 0f, 19f), new Vector3(5f, 3f, 4f), banco, t);
            House("Maison_4", new Vector3(9f, 0f, 19f), new Vector3(4f, 3f, 6f), banco, t);

            // Cases rondes du quartier ouest.
            Hut("Case_1", new Vector3(-21f, 0f, -10f), banco, t);
            Hut("Case_2", new Vector3(-24f, 0f, 2f), banco, t);
            Hut("Case_3", new Vector3(-20f, 0f, 14f), banco, t);

            // Murets bas : accroupi (C) derrière, on est invisible.
            Block("Muret_1", new Vector3(0f, 0.6f, -4f), new Vector3(8f, 1.2f, 0.6f), banco, 0, t);
            Block("Muret_2", new Vector3(-4f, 0.6f, 12f), new Vector3(0.6f, 1.2f, 6f), banco, 0, t);
            Block("Muret_3", new Vector3(4f, 0.6f, 24f), new Vector3(6f, 1.2f, 0.6f), banco, 0, t);

            // Étals du marché.
            Stall("Etal_1", new Vector3(-5f, 0f, -12f), new Color(0.75f, 0.15f, 0.12f), t);
            Stall("Etal_2", new Vector3(5f, 0f, -12f), new Color(0.95f, 0.7f, 0.15f), t);
            Stall("Etal_3", new Vector3(16f, 0f, 7f), new Color(0.15f, 0.45f, 0.3f), t);

            // Tour de guet à escalader (layer Climbable) : avance contre elle pour grimper.
            Block("Tour_de_guet", new Vector3(15f, 4f, -6f), new Vector3(3f, 8f, 3f), wood, climbLayer, t);

            foreach (Vector3 p in new[] { new Vector3(-14f, 0f, -20f), new Vector3(12f, 0f, -24f), new Vector3(-27f, 0f, 24f),
                                          new Vector3(26f, 0f, -20f), new Vector3(-12f, 0f, 30f), new Vector3(30f, 0f, 6f) })
                Palm(p, t);

            Material clay = Mat("Poterie", new Color(0.55f, 0.27f, 0.15f));
            foreach (Vector3 p in new[] { new Vector3(-18.5f, 0.3f, -8f), new Vector3(-19f, 0.3f, -7.3f), new Vector3(-21.5f, 0.3f, 4.2f),
                                          new Vector3(-6f, 0.3f, -12.8f), new Vector3(-17.8f, 0.3f, 15.5f) })
                Visual(PrimitiveType.Sphere, "Jarre", t, p, new Vector3(0.55f, 0.6f, 0.55f), clay);

            // Remparts autour du niveau.
            Block("Rempart_Nord", new Vector3(0f, 2f, 35f), new Vector3(70f, 4f, 1f), banco, 0, t);
            Block("Rempart_Sud", new Vector3(0f, 2f, -35f), new Vector3(70f, 4f, 1f), banco, 0, t);
            Block("Rempart_Est", new Vector3(35f, 2f, 0f), new Vector3(1f, 4f, 70f), banco, 0, t);
            Block("Rempart_Ouest", new Vector3(-35f, 2f, 0f), new Vector3(1f, 4f, 70f), banco, 0, t);
        }

        static void House(string name, Vector3 foot, Vector3 size, Material banco, Transform parent)
        {
            GameObject house = Block(name, foot + Vector3.up * size.y / 2f, size, banco, 0, parent);
            Roof(name + "_Toit", parent, foot + Vector3.up * size.y, new Vector3(size.x * 1.5f, 1.8f, size.z * 1.5f),
                4, 45f, Mat("Chaume", new Color(0.62f, 0.5f, 0.28f)));
            // Porte sombre, pour l'ambiance.
            Visual(PrimitiveType.Cube, "Porte", house.transform, new Vector3(0f, -0.2f, -0.51f), new Vector3(0.22f, 0.6f, 0.02f),
                Mat("Ombre", new Color(0.12f, 0.07f, 0.05f)));
        }

        static void Hut(string name, Vector3 foot, Material banco, Transform parent)
        {
            GameObject hut = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hut.name = name;
            hut.transform.SetParent(parent);
            hut.transform.position = foot + Vector3.up * 1.5f;
            hut.transform.localScale = new Vector3(4f, 1.5f, 4f);
            hut.GetComponent<Renderer>().sharedMaterial = banco;
            // La capsule par défaut d'un cylindre est trop ronde : collision exacte à la place.
            Object.DestroyImmediate(hut.GetComponent<Collider>());
            hut.AddComponent<MeshCollider>();
            Roof(name + "_Toit", parent, foot + Vector3.up * 3f, new Vector3(5.4f, 2.4f, 5.4f), 16, 0f,
                Mat("Chaume", new Color(0.62f, 0.5f, 0.28f)));
        }

        static void Stall(string name, Vector3 foot, Color cloth, Transform parent)
        {
            Transform stall = new GameObject(name).transform;
            stall.SetParent(parent);
            stall.position = foot;
            Material wood = Mat("Bois", new Color(0.36f, 0.24f, 0.13f));
            foreach (Vector2 c in new[] { new Vector2(-1f, -0.6f), new Vector2(1f, -0.6f), new Vector2(-1f, 0.6f), new Vector2(1f, 0.6f) })
                Block("Poteau", foot + new Vector3(c.x, 0.85f, c.y), new Vector3(0.12f, 1.7f, 0.12f), wood, 0, stall);
            Block("Table", foot + new Vector3(0f, 0.9f, 0f), new Vector3(2f, 0.08f, 1.2f), wood, 0, stall);
            Visual(PrimitiveType.Cube, "Toile", stall, new Vector3(0f, 1.72f, 0f), new Vector3(2.5f, 0.05f, 1.7f),
                Mat("Toile_" + name, cloth));
            Material fruit = Mat("Fruits", new Color(0.95f, 0.55f, 0.1f));
            for (int i = 0; i < 4; i++)
                Visual(PrimitiveType.Sphere, "Fruit", stall, new Vector3(-0.6f + i * 0.35f, 1.02f, -0.2f), Vector3.one * 0.2f, fruit);
        }

        static void Palm(Vector3 foot, Transform parent)
        {
            GameObject trunk = Block("Palmier", foot + Vector3.up * 3f, new Vector3(0.4f, 6f, 0.4f),
                Mat("Tronc", new Color(0.45f, 0.33f, 0.2f)), 0, parent);
            Material leaf = Mat("Palme", new Color(0.25f, 0.45f, 0.15f));
            for (int i = 0; i < 6; i++)
            {
                GameObject frond = Visual(PrimitiveType.Cube, "Palme", parent, foot + Vector3.up * 6f,
                    new Vector3(0.6f, 0.06f, 3f), leaf);
                frond.transform.rotation = Quaternion.Euler(20f, i * 60f, 0f);
                frond.transform.position += frond.transform.forward * 1.3f;
            }
        }

        // ---------------------------------------------------------------- Entrepôt (mission 3)
        static void BuildWarehouse()
        {
            Transform t = new GameObject("Entrepot_des_Chaines").transform;
            Material wall = Mat("Banco_Sombre", new Color(0.42f, 0.24f, 0.15f));
            Block("Mur_Nord", new Vector3(27f, 1.75f, 30f), new Vector3(10f, 3.5f, 0.5f), wall, 0, t);
            Block("Mur_Sud", new Vector3(27f, 1.75f, 18f), new Vector3(10f, 3.5f, 0.5f), wall, 0, t);
            Block("Mur_Est", new Vector3(32f, 1.75f, 24f), new Vector3(0.5f, 3.5f, 12f), wall, 0, t);
            Block("Mur_Ouest_1", new Vector3(22f, 1.75f, 20f), new Vector3(0.5f, 3.5f, 4f), wall, 0, t);
            Block("Mur_Ouest_2", new Vector3(22f, 1.75f, 28f), new Vector3(0.5f, 3.5f, 4f), wall, 0, t);
            Block("Toit", new Vector3(27f, 3.65f, 24f), new Vector3(10.5f, 0.3f, 12.5f), Mat("Bois", new Color(0.36f, 0.24f, 0.13f)), 0, t);

            GameObject lamp = new GameObject("Lampe");
            lamp.transform.SetParent(t);
            lamp.transform.position = new Vector3(27f, 3f, 24f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.3f);
            light.range = 9f;
            light.intensity = 2f;

            GameObject zone = new GameObject("Zone_Entrepot");
            zone.layer = IgnoreRaycastLayer;
            zone.transform.SetParent(t);
            zone.transform.position = new Vector3(27f, 1.5f, 24f);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(8.5f, 3f, 10.5f);
            QuestZone questZone = zone.AddComponent<QuestZone>();
            var so = new SerializedObject(questZone);
            so.FindProperty("questId").stringValue = "Q03";
            so.FindProperty("zoneId").stringValue = "Entrepot";
            so.ApplyModifiedPropertiesWithoutUndo();

            Captive("Captif_1", new Vector3(25f, 0f, 26f), 160f, t);
            Captive("Captif_2", new Vector3(28f, 0f, 26.5f), 200f, t);
            Captive("Captif_3", new Vector3(30.5f, 0f, 21.5f), 260f, t);
        }

        static void Captive(string name, Vector3 foot, float yaw, Transform parent)
        {
            GameObject captive = new GameObject(name);
            captive.transform.SetParent(parent);
            captive.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));
            CapsuleCollider col = captive.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.height = 1.2f;
            col.radius = 0.35f;

            ProceduralAnimator anim = Humanoid(captive.transform, "Captif", new Color(0.33f, 0.2f, 0.13f),
                new Color(0.33f, 0.2f, 0.13f), new Color(0.33f, 0.2f, 0.13f), new Color(0.5f, 0.4f, 0.3f),
                new Color(0.78f, 0.7f, 0.55f), 0.16f, false, 0, 0.95f);
            anim.kneel = true;
            anim.footstepVolume = 0f;

            GameObject chains = Visual(PrimitiveType.Cube, "Chaines", captive.transform, new Vector3(0f, 0.4f, 0.32f),
                new Vector3(0.5f, 0.1f, 0.12f), Mat("Fer", new Color(0.2f, 0.2f, 0.22f)));

            QuestInteractable interactable = captive.AddComponent<QuestInteractable>();
            var so = new SerializedObject(interactable);
            so.FindProperty("questId").stringValue = "Q03";
            so.FindProperty("goalType").enumValueIndex = (int)QuestGoalType.TalkToNPC;
            so.FindProperty("targetId").stringValue = "Captif";
            so.FindProperty("prompt").stringValue = "Libérer le captif";
            so.FindProperty("hideUntilQuestActive").boolValue = false;
            so.FindProperty("destroyOnUse").boolValue = false;
            SerializedProperty disable = so.FindProperty("disableOnUse");
            disable.arraySize = 1;
            disable.GetArrayElementAtIndex(0).objectReferenceValue = chains;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Lettres (mission 2)
        static void BuildLetters()
        {
            Transform t = new GameObject("Lettres_du_Traitre").transform;
            Letter("Lettre_Tour", new Vector3(15f, 8.15f, -6f), t);    // en haut de la tour : il faut grimper
            Letter("Lettre_Maison", new Vector3(-13f, 0.15f, 19f), t); // derrière une maison, près du capitaine
            Letter("Lettre_Etal", new Vector3(16f, 1f, 7f), t);        // sur l'étal gardé
        }

        static void Letter(string name, Vector3 position, Transform parent)
        {
            GameObject letter = new GameObject(name);
            letter.layer = IgnoreRaycastLayer;
            letter.transform.SetParent(parent);
            letter.transform.position = position;
            BoxCollider box = letter.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one;

            Visual(PrimitiveType.Cube, "Papier", letter.transform, Vector3.zero, new Vector3(0.32f, 0.02f, 0.24f),
                Mat("Parchemin", new Color(0.93f, 0.86f, 0.68f)));
            Visual(PrimitiveType.Sphere, "Sceau", letter.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.07f, 0.02f, 0.07f),
                Mat("Rouge", new Color(0.75f, 0.1f, 0.1f)));

            GameObject glow = new GameObject("Lueur");
            glow.transform.SetParent(letter.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.4f);
            light.range = 2.5f;
            light.intensity = 2f;

            QuestInteractable interactable = letter.AddComponent<QuestInteractable>();
            var so = new SerializedObject(interactable);
            so.FindProperty("questId").stringValue = "Q02";
            so.FindProperty("goalType").enumValueIndex = (int)QuestGoalType.CollectIntel;
            so.FindProperty("targetId").stringValue = "Lettre";
            so.FindProperty("prompt").stringValue = "Ramasser la lettre";
            so.FindProperty("hideUntilQuestActive").boolValue = true;
            so.FindProperty("destroyOnUse").boolValue = true;
            so.FindProperty("spin").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Personnages 3D
        // Un personnage = des pivots (hanches, torse, tête, bras, jambes) + des formes simples.
        // ProceduralAnimator fait tourner les pivots pour l'animer.
        static ProceduralAnimator Humanoid(Transform parent, string id, Color skin, Color top, Color legs, Color accent,
            Color? skirt, float skirtLength, bool hat, int weapon, float scale)
        {
            Material mSkin = Mat(id + "_Peau", skin);
            Material mTop = Mat(id + "_Haut", top);
            Material mLegs = Mat(id + "_Jambes", legs);
            Material mAccent = Mat(id + "_Accent", accent);
            Material mDark = Mat("Cuir", new Color(0.15f, 0.1f, 0.07f));

            Transform model = Pivot("Modele", parent, Vector3.zero);
            model.localScale = Vector3.one * scale;

            Transform hips = Pivot("Hanches", model, new Vector3(0f, 0.95f, 0f));
            Transform legL = Pivot("JambeG", hips, new Vector3(-0.12f, 0f, 0f));
            Transform legR = Pivot("JambeD", hips, new Vector3(0.12f, 0f, 0f));
            foreach (Transform leg in new[] { legL, legR })
            {
                Visual(PrimitiveType.Cube, "Jambe", leg, new Vector3(0f, -0.45f, 0f), new Vector3(0.17f, 0.9f, 0.19f), mLegs);
                Visual(PrimitiveType.Cube, "Pied", leg, new Vector3(0f, -0.9f, 0.06f), new Vector3(0.17f, 0.08f, 0.28f), mDark);
            }
            if (skirt.HasValue)
                Visual(PrimitiveType.Cylinder, "Pagne", hips, new Vector3(0f, -skirtLength, 0f),
                    new Vector3(0.52f, skirtLength, 0.42f), Mat(id + "_Pagne", skirt.Value));

            Transform torso = Pivot("Torse", hips, new Vector3(0f, 0.02f, 0f));
            Visual(PrimitiveType.Cube, "Buste", torso, new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.58f, 0.26f), mTop);
            Visual(PrimitiveType.Cube, "Ceinture", torso, new Vector3(0f, 0.03f, 0f), new Vector3(0.46f, 0.08f, 0.28f), mAccent);

            Transform head = Pivot("Tete", torso, new Vector3(0f, 0.62f, 0f));
            Visual(PrimitiveType.Sphere, "Visage", head, new Vector3(0f, 0.14f, 0f), new Vector3(0.26f, 0.3f, 0.27f), mSkin);
            Visual(PrimitiveType.Cube, "Nez", head, new Vector3(0f, 0.12f, 0.14f), new Vector3(0.05f, 0.06f, 0.05f), mSkin);
            Visual(PrimitiveType.Cube, "Bandeau", head, new Vector3(0f, 0.22f, 0f), new Vector3(0.28f, 0.06f, 0.29f), mAccent);
            if (hat)
            {
                Visual(PrimitiveType.Cylinder, "Bord", head, new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.02f, 0.44f), mDark);
                Visual(PrimitiveType.Cylinder, "Calotte", head, new Vector3(0f, 0.38f, 0f), new Vector3(0.26f, 0.08f, 0.26f), mDark);
            }

            Transform armL = Pivot("BrasG", torso, new Vector3(-0.29f, 0.55f, 0f));
            Transform armR = Pivot("BrasD", torso, new Vector3(0.29f, 0.55f, 0f));
            foreach (Transform arm in new[] { armL, armR })
            {
                Visual(PrimitiveType.Cube, "Bras", arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.12f, 0.58f, 0.13f), mSkin);
                Visual(PrimitiveType.Sphere, "Main", arm, new Vector3(0f, -0.6f, 0f), Vector3.one * 0.12f, mSkin);
            }
            Visual(PrimitiveType.Cube, "Epaule", armL, new Vector3(0f, -0.08f, 0f), new Vector3(0.15f, 0.18f, 0.16f), mTop);
            Visual(PrimitiveType.Cube, "Epaule", armR, new Vector3(0f, -0.08f, 0f), new Vector3(0.15f, 0.18f, 0.16f), mTop);

            if (weapon > 0)
            {
                Transform grip = Pivot("Arme", armR, new Vector3(0f, -0.6f, 0f));
                Material metal = Mat("Metal", new Color(0.75f, 0.75f, 0.78f));
                if (weapon == 1) // coupe-coupe de l'Agojie
                {
                    Visual(PrimitiveType.Cube, "Manche", grip, new Vector3(0f, 0f, 0.06f), new Vector3(0.04f, 0.04f, 0.16f), mDark);
                    Visual(PrimitiveType.Cube, "Lame", grip, new Vector3(0f, 0.02f, 0.45f), new Vector3(0.025f, 0.1f, 0.62f), metal);
                }
                else if (weapon == 2) // gourdin des gardes
                {
                    Visual(PrimitiveType.Cube, "Gourdin", grip, new Vector3(0f, 0f, 0.32f), new Vector3(0.08f, 0.08f, 0.7f),
                        Mat("Bois", new Color(0.36f, 0.24f, 0.13f)));
                }
                else // sabre du capitaine
                {
                    Visual(PrimitiveType.Cube, "Garde", grip, new Vector3(0f, 0f, 0.08f), new Vector3(0.14f, 0.03f, 0.03f), metal);
                    Visual(PrimitiveType.Cube, "Lame", grip, new Vector3(0f, 0f, 0.48f), new Vector3(0.02f, 0.06f, 0.78f), metal);
                }
            }

            ProceduralAnimator anim = model.gameObject.AddComponent<ProceduralAnimator>();
            anim.hips = hips;
            anim.torso = torso;
            anim.head = head;
            anim.armL = armL;
            anim.armR = armR;
            anim.legL = legL;
            anim.legR = legR;
            return anim;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        // ---------------------------------------------------------------- Joueur
        static GameObject BuildPlayer(Transform cam)
        {
            GameObject player = new GameObject("Agojie");
            player.layer = playerLayer;
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.05f, -22f);

            // Pivot aux pieds : la capsule de collision monte de 0 à 2 m.
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;

            ProceduralAnimator anim = Humanoid(player.transform, "Agojie", new Color(0.36f, 0.22f, 0.14f),
                new Color(0.14f, 0.16f, 0.4f), new Color(0.36f, 0.22f, 0.14f), new Color(0.75f, 0.1f, 0.1f),
                new Color(0.9f, 0.88f, 0.8f), 0.2f, false, 1, 1f);
            anim.footstepVolume = 0.3f;

            player.AddComponent<HealthComponent>();

            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            movement.cameraTransform = cam;
            movement.climbableLayer = 1 << climbLayer;
            movement.enemyLayers = 1 << enemyLayer;

            GameObject attackPoint = new GameObject("AttackPoint");
            attackPoint.transform.SetParent(player.transform, false);
            attackPoint.transform.localPosition = new Vector3(0f, 1f, 1f);

            PlayerCombat combat = player.AddComponent<PlayerCombat>();
            combat.attackPoint = attackPoint.transform;
            combat.enemyLayers = 1 << enemyLayer;

            player.AddComponent<PlayerInteractor>();
            player.AddComponent<PlayerDeathHandler>();
            return player;
        }

        static void BuildCamera(Camera cam, Transform player)
        {
            cam.transform.position = player.position + new Vector3(0f, 3f, -4f);
            ThirdPersonCamera tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
            tpc.target = player;
            tpc.collisionLayers = ~((1 << playerLayer) | (1 << enemyLayer) | (1 << IgnoreRaycastLayer));
        }

        // ---------------------------------------------------------------- Ennemis
        static void BuildEnemies()
        {
            Transform root = new GameObject("Ennemis").transform;
            Enemy("Garde_1", root, "", new Vector3(-4f, 0f, 1f), new Vector3(4f, 0f, 1f));
            Enemy("Garde_2", root, "", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 16f), new Vector3(-1f, 0f, 22f));
            Enemy("Garde_3", root, "", new Vector3(13f, 0f, 0f), new Vector3(13f, 0f, 14f));
            Enemy("Capitaine", root, "Capitaine", new Vector3(-5f, 0f, 28f), new Vector3(5f, 0f, 28f));
            Enemy("Garde_Entrepot_1", root, "", new Vector3(19f, 0f, 14f), new Vector3(19f, 0f, 32f));
            Enemy("Garde_Entrepot_2", root, "", new Vector3(24f, 0f, 20f), new Vector3(30f, 0f, 28f), new Vector3(24f, 0f, 28f));
        }

        static void Enemy(string name, Transform parent, string questId, params Vector3[] waypoints)
        {
            bool captain = questId != "";
            GameObject enemy = new GameObject(name);
            enemy.layer = enemyLayer;
            enemy.transform.SetParent(parent);
            enemy.transform.position = waypoints[0];

            CapsuleCollider col = enemy.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.4f;

            NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.3f;

            ProceduralAnimator anim = captain
                ? Humanoid(enemy.transform, "Capitaine", new Color(0.85f, 0.68f, 0.55f), new Color(0.18f, 0.1f, 0.1f),
                    new Color(0.08f, 0.08f, 0.08f), new Color(0.7f, 0.55f, 0.15f), null, 0f, true, 3, 1.1f)
                : Humanoid(enemy.transform, "Garde", new Color(0.45f, 0.3f, 0.2f), new Color(0.6f, 0.52f, 0.36f),
                    new Color(0.3f, 0.22f, 0.15f), new Color(0.45f, 0.15f, 0.1f), null, 0f, false, 2, 1f);
            anim.footstepVolume = 0.18f;

            HealthComponent health = enemy.AddComponent<HealthComponent>();
            if (captain) SetInt(health, "maxHealth", 150);

            EnemyPerception perception = enemy.AddComponent<EnemyPerception>();
            SetMask(perception, "targetMask", 1 << playerLayer);
            SetMask(perception, "obstacleMask", (1 << 0) | (1 << climbLayer));

            EnemyController controller = enemy.AddComponent<EnemyController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playerLayer").intValue = 1 << playerLayer;
            so.FindProperty("questTargetId").stringValue = questId;
            SerializedProperty list = so.FindProperty("patrolWaypoints");
            list.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform point = new GameObject(name + "_Point_" + (i + 1)).transform;
                point.SetParent(parent);
                point.position = waypoints[i];
                list.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildStealthMeter(enemy.transform, perception);
        }

        // Jauge jaune/rouge au-dessus de la tête : à quel point l'ennemi t'a repérée.
        static void BuildStealthMeter(Transform enemy, EnemyPerception perception)
        {
            GameObject go = new GameObject("Jauge_Detection", typeof(RectTransform));
            go.transform.SetParent(enemy, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(0f, 2.5f, 0f);
            rt.sizeDelta = new Vector2(100f, 12f);
            rt.localScale = Vector3.one * 0.01f;

            CanvasGroup group = go.AddComponent<CanvasGroup>();
            go.AddComponent<FaceCamera>();

            Image background = UIImage("Fond", go.transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(background.rectTransform);
            Image fill = UIImage("Remplissage", go.transform, Color.yellow);
            Stretch(fill.rectTransform);
            MakeFilled(fill, 0f);

            StealthDetectionUI ui = go.AddComponent<StealthDetectionUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("enemyPerception").objectReferenceValue = perception;
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.FindProperty("awarenessFillBar").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- PNJ & données
        static void BuildNpc(DialogueData dialogue)
        {
            GameObject npc = new GameObject("PNJ_Hounon");
            npc.transform.position = new Vector3(2.5f, 0f, -18f);
            npc.transform.rotation = Quaternion.LookRotation(new Vector3(-2.5f, 0f, -4f));

            CapsuleCollider col = npc.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.4f;

            ProceduralAnimator anim = Humanoid(npc.transform, "Hounon", new Color(0.3f, 0.19f, 0.12f),
                new Color(0.95f, 0.94f, 0.9f), new Color(0.3f, 0.19f, 0.12f), new Color(0.2f, 0.25f, 0.6f),
                new Color(0.95f, 0.94f, 0.9f), 0.42f, false, 0, 1f);
            anim.footstepVolume = 0f;

            NPCDialogue talk = npc.AddComponent<NPCDialogue>();
            var so = new SerializedObject(talk);
            so.FindProperty("npcId").stringValue = "Hounon";
            so.FindProperty("npcName").stringValue = "la Hounon";
            so.FindProperty("dialogue").objectReferenceValue = dialogue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static List<QuestData> CreateQuests()
        {
            var quests = new List<QuestData>
            {
                Quest("Q01", "Le capitaine du marché",
                    "Un capitaine négrier et ses gardes occupent le marché. Élimine-le sans te faire voir.", "Q02",
                    Goal("Éliminer le capitaine négrier", QuestGoalType.AssassinateTarget, "Capitaine", 1)),
                Quest("Q02", "Les lettres du traître",
                    "Le capitaine parlait d'un traître qui vend des captifs. Trouve ses trois lettres cachées.", "Q03",
                    Goal("Trouver les lettres (l'une est en haut de la tour de guet)", QuestGoalType.CollectIntel, "Lettre", 3)),
                Quest("Q03", "L'entrepôt des chaînes",
                    "Les lettres mènent à l'entrepôt du nord-est. Entre et libère les captifs.", "",
                    Goal("Infiltrer l'entrepôt au nord-est", QuestGoalType.InfiltrateZone, "Entrepot", 1),
                    Goal("Libérer les captifs (E)", QuestGoalType.TalkToNPC, "Captif", 3)),
            };
            return quests;
        }

        static QuestGoal Goal(string description, QuestGoalType type, string target, int amount)
        {
            return new QuestGoal { goalDescription = description, goalType = type, targetIdentifier = target, requiredAmount = amount };
        }

        static QuestData Quest(string id, string title, string description, string next, params QuestGoal[] goals)
        {
            string path = Root + "/Data/" + id + ".asset";
            AssetDatabase.DeleteAsset(path);
            QuestData quest = ScriptableObject.CreateInstance<QuestData>();
            quest.questId = id;
            quest.questTitle = title;
            quest.questDescription = description;
            quest.nextQuestId = next;
            quest.goals.AddRange(goals);
            AssetDatabase.CreateAsset(quest, path);
            return quest;
        }

        static DialogueData CreateDialogue()
        {
            string path = Root + "/Data/D01_Hounon.asset";
            AssetDatabase.DeleteAsset(path);
            DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
            dialogue.dialogueId = "hounon_intro";

            DialogueNode intro = new DialogueNode
            {
                speakerName = "La Hounon",
                dialogueText = "Agojie, le roi a besoin de ta lame. Un capitaine négrier s'est installé au marché, " +
                               "protégé par ses gardes. Il ne doit pas voir le soleil se lever."
            };
            intro.choices.Add(new DialogueChoice { choiceText = "J'accepte. Le roi peut compter sur moi.", targetNodeIndex = 1, triggerQuestId = "Q01" });
            intro.choices.Add(new DialogueChoice { choiceText = "Pas encore, je dois me préparer.", targetNodeIndex = -1 });

            DialogueNode advice = new DialogueNode
            {
                speakerName = "La Hounon",
                dialogueText = "Reste dans l'ombre : accroupis-toi (C) derrière les murets. Ne cours pas, ils t'entendraient. " +
                               "Approche-toi dans leur dos et frappe (F). Que les vodun guident tes pas."
            };

            dialogue.nodes.Add(intro);
            dialogue.nodes.Add(advice);
            AssetDatabase.CreateAsset(dialogue, path);
            return dialogue;
        }

        static void BuildManagers(List<QuestData> questList)
        {
            GameObject managers = new GameObject("Managers");
            QuestManager questManager = managers.AddComponent<QuestManager>();
            var so = new SerializedObject(questManager);
            SerializedProperty quests = so.FindProperty("allQuests");
            quests.arraySize = questList.Count;
            for (int i = 0; i < questList.Count; i++)
                quests.GetArrayElementAtIndex(i).objectReferenceValue = questList[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            managers.AddComponent<DialogueManager>();
            managers.AddComponent<PauseMenuUI>();
            managers.AddComponent<AudioManager>();
        }

        // ---------------------------------------------------------------- HUD
        static void BuildHud(GameObject player)
        {
            GameObject canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform hud = canvasGo.transform;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Barre de vie (haut gauche)
            Image healthBack = UIImage("Vie_Fond", hud, new Color(0f, 0f, 0f, 0.6f));
            Place(healthBack.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(420f, 26f));
            Image ghost = UIImage("Vie_Fantome", healthBack.transform, new Color(1f, 0.85f, 0.55f));
            Stretch(ghost.rectTransform);
            MakeFilled(ghost, 1f);
            Image fill = UIImage("Vie", healthBack.transform, new Color(0.8f, 0.1f, 0.1f));
            Stretch(fill.rectTransform);
            MakeFilled(fill, 1f);
            HealthBarUI healthBar = healthBack.gameObject.AddComponent<HealthBarUI>();
            var hb = new SerializedObject(healthBar);
            hb.FindProperty("targetHealth").objectReferenceValue = player.GetComponent<HealthComponent>();
            hb.FindProperty("healthFillImage").objectReferenceValue = fill;
            hb.FindProperty("damageGhostImage").objectReferenceValue = ghost;
            hb.ApplyModifiedPropertiesWithoutUndo();

            // Suivi de mission (haut droite)
            RectTransform questBox = UIRect("Mission", hud);
            Place(questBox, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(560f, 120f));
            CanvasGroup questGroup = questBox.gameObject.AddComponent<CanvasGroup>();
            Text questTitle = UIText("Titre", questBox, "", 32, TextAnchor.UpperRight);
            questTitle.color = new Color(1f, 0.82f, 0.3f);
            Place(questTitle.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(560f, 44f));
            Text questGoal = UIText("Objectif", questBox, "", 24, TextAnchor.UpperRight);
            Place(questGoal.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -48f), new Vector2(560f, 70f));
            QuestTrackerUI tracker = questBox.gameObject.AddComponent<QuestTrackerUI>();
            var qt = new SerializedObject(tracker);
            qt.FindProperty("questTitleText").objectReferenceValue = questTitle;
            qt.FindProperty("questObjectiveText").objectReferenceValue = questGoal;
            qt.FindProperty("containerGroup").objectReferenceValue = questGroup;
            qt.ApplyModifiedPropertiesWithoutUndo();

            // Message d'interaction, mort, aide des touches
            Text prompt = UIText("Prompt_Interaction", hud, "", 28, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 340f), new Vector2(800f, 44f));
            player.GetComponent<PlayerInteractor>().promptText = prompt;

            Text death = UIText("Message_Mort", hud, "", 60, TextAnchor.MiddleCenter);
            death.color = new Color(0.9f, 0.2f, 0.2f);
            Place(death.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 120f));
            player.GetComponent<PlayerDeathHandler>().deathText = death;

            Text help = UIText("Aide_Touches", hud,
                "ZQSD bouger · Maj courir · C s'accroupir · Ctrl esquive · Espace sauter · " +
                "Clic G combo · Clic D lourd · F assassiner · E parler/ramasser/libérer · Échap pause", 18, TextAnchor.LowerLeft);
            Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 20f), new Vector2(1400f, 30f));

            BuildDialogueBox(hud);
        }

        static void BuildDialogueBox(Transform hud)
        {
            Image panel = UIImage("Dialogue", hud, new Color(0f, 0f, 0f, 0.78f));
            Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 270f));

            Text speaker = UIText("Nom", panel.transform, "", 30, TextAnchor.UpperLeft);
            speaker.color = new Color(1f, 0.82f, 0.3f);
            Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -18f), new Vector2(800f, 40f));

            Text body = UIText("Texte", panel.transform, "", 25, TextAnchor.UpperLeft);
            Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -62f), new Vector2(1140f, 100f));

            RectTransform choices = UIRect("Choix", panel.transform);
            Place(choices, new Vector2(0f, 0f), new Vector2(30f, 16f), new Vector2(1140f, 84f));
            VerticalLayoutGroup layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Image templateImage = UIImage("Choix_Modele", choices, new Color(1f, 1f, 1f, 0.12f));
            Button template = templateImage.gameObject.AddComponent<Button>();
            LayoutElement element = templateImage.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 36f;
            Text label = UIText("Texte", templateImage.transform, "Choix", 23, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(14f, 0f);

            DialogueUI ui = hud.gameObject.AddComponent<DialogueUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("speakerText").objectReferenceValue = speaker;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("choicesContainer").objectReferenceValue = choices;
            so.FindProperty("choiceTemplate").objectReferenceValue = template;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Outils
        // Cône (ou pyramide avec 4 côtés) pour les toits de chaume. Enregistré comme asset.
        static Mesh ConeMesh(int segments)
        {
            string path = Root + "/Meshes/Cone_" + segments + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                return existing;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                AddTriangle(vertices, triangles, apex, b1, b0);        // côté, vu de l'extérieur
                AddTriangle(vertices, triangles, Vector3.zero, b0, b1); // dessous
            }
            Mesh mesh = new Mesh { name = "Cone_" + segments };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            triangles.Add(vertices.Count); vertices.Add(a);
            triangles.Add(vertices.Count); vertices.Add(b);
            triangles.Add(vertices.Count); vertices.Add(c);
        }

        static GameObject Roof(string name, Transform parent, Vector3 basePosition, Vector3 scale, int segments, float yaw, Material mat)
        {
            GameObject roof = new GameObject(name);
            roof.transform.SetParent(parent);
            roof.transform.SetPositionAndRotation(basePosition, Quaternion.Euler(0f, yaw, 0f));
            roof.transform.localScale = scale;
            roof.AddComponent<MeshFilter>().sharedMesh = ConeMesh(segments);
            roof.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return roof;
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing != -1)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            Debug.LogError("[Agojie] Plus de layer libre pour " + name);
            return 0;
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        static Font LoadFont()
        {
            foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    Font f = Resources.GetBuiltinResource<Font>(name);
                    if (f != null) return f;
                }
                catch (System.Exception) { }
            }
            return null;
        }

        static Material Mat(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.1f); // mat, pas plastique (URP)
            mat.SetFloat("_Glossiness", 0.1f); // idem (pipeline Built-in)
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject Block(string name, Vector3 position, Vector3 scale, Material mat, int layer, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // Forme visible sans collider (la collision est gérée par le parent).
        static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.layer = parent.gameObject.layer;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void SetMask(Object target, string property, int mask)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static RectTransform UIRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Image UIImage(string name, Transform parent, Color color)
        {
            Image image = UIRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        static void MakeFilled(Image image, float amount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = amount;
        }

        static Text UIText(string name, Transform parent, string content, int size, TextAnchor alignment)
        {
            Text text = UIRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
