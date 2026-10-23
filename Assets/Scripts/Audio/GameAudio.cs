using System.Collections.Generic;
using UnityEngine;
using Agojie.Quests;

namespace Agojie.Audio
{
    public enum Sfx
    {
        Step, Swing, Dodge, Hit, HeavyHit, Assassinate, Hurt, Death, Alert, Pickup, QuestStart, QuestComplete
    }

    /// <summary>Raccourci : GameAudio.Play(Sfx.Hit, position). Ne fait rien s'il n'y a pas d'AudioManager.</summary>
    public static class GameAudio
    {
        public static void Play(Sfx sfx, Vector3 position, float volume = 1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(sfx, position, volume);
        }

        public static void Play2D(Sfx sfx, float volume = 1f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFlat(sfx, volume);
        }
    }

    /// <summary>
    /// Joue les bruitages et la musique d'ambiance.
    /// Tous les sons sont fabriqués par le code (ProceduralSounds) : aucun fichier audio à importer.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private float musicVolume = 0.35f;
        [SerializeField] private float sfxVolume = 0.9f;
        [SerializeField] private int voices = 16; // nombre de sons joués en même temps

        private Dictionary<Sfx, AudioClip> clips;
        private AudioSource[] pool;
        private AudioSource flat;
        private int nextVoice;
        private QuestManager quests;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            clips = ProceduralSounds.CreateAll();

            // Sources 3D : un coup lointain s'entend moins fort.
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                GameObject voice = new GameObject("Voix_" + i);
                voice.transform.SetParent(transform);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f;
                source.maxDistance = 35f;
                pool[i] = source;
            }

            flat = gameObject.AddComponent<AudioSource>();
            flat.playOnAwake = false;
            flat.spatialBlend = 0f;

            AudioSource music = gameObject.AddComponent<AudioSource>();
            music.clip = ProceduralSounds.CreateAmbientLoop();
            music.loop = true;
            music.volume = musicVolume;
            music.spatialBlend = 0f;
            music.Play();
        }

        private void Start()
        {
            quests = QuestManager.Instance;
            if (quests == null) return;
            quests.OnQuestStarted += HandleQuestStarted;
            quests.OnQuestCompleted += HandleQuestCompleted;
        }

        private void OnDestroy()
        {
            if (quests == null) return;
            quests.OnQuestStarted -= HandleQuestStarted;
            quests.OnQuestCompleted -= HandleQuestCompleted;
        }

        private void HandleQuestStarted(QuestData quest) => PlayFlat(Sfx.QuestStart, 0.8f);
        private void HandleQuestCompleted(QuestData quest) => PlayFlat(Sfx.QuestComplete, 0.9f);

        public void PlayAt(Sfx sfx, Vector3 position, float volume)
        {
            AudioSource source = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            source.transform.position = position;
            source.clip = clips[sfx];
            source.volume = volume * sfxVolume;
            source.pitch = Random.Range(0.92f, 1.08f); // petite variation : moins répétitif
            source.Play();
        }

        public void PlayFlat(Sfx sfx, float volume)
        {
            flat.PlayOneShot(clips[sfx], volume * sfxVolume);
        }
    }
}
