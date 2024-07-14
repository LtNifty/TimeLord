using System.Collections;
using ThunderRoad;
using UnityEngine;
using HarmonyLib;
using System.IO;
using UnityEngine.Networking;
using ThunderRoad.Skill.SpellPower;

namespace Quicksilver
{
    public enum QuicksilverMusic
    {
        None,
        SweetDreams,
        TimeInABottle,
        Custom
    }

    public class QuicksilverModule : ThunderScript
    {
        public static ModOptionFloat[] ZeroToOneHundered()
        {
            ModOptionFloat[] options = new ModOptionFloat[101];
            float val = 0;
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = new ModOptionFloat(val.ToString("0.0"), val);
                val += 1f;
            }
            return options;
        }

        public static ModOptionFloat[] CustomTimescaleModOptions()
        {
            ModOptionFloat[] options = new ModOptionFloat[105];
            float val = 0.005f;
            for (int i = 0; i < options.Length; i++)
            {
                if (i < 5)
                {
                    options[i] = new ModOptionFloat(val.ToString("0.000"), val);
                    val += 0.001f;
                } 
                else
                {
                    options[i] = new ModOptionFloat(val.ToString("0.00"), val);
                    val += 0.01f;
                }
            }
            return options;
        }

        [ModOption(
            name: "Use Time Lord Mod", 
            tooltip: "Turns on/off the Time Lord mod.", 
            defaultValueIndex = 1, 
            order = 0)]
        public static void TimeLordChange(bool option)
        {
            GameManager.options.crouchOnJump = !option;
            useTimeLord = option;
        }
        private static bool useTimeLord;

        [ModOption(
            name: "Use Instant Stop",
            tooltip: "Determines if you want to want to instantly exit out of slow-mo & Quicksilver.",
            defaultValueIndex = 1,
            order = 1)]
        public static bool useInstantStop;

        [ModOption(
            name: "Use Lightning Indicators",
            tooltip: "Determines if you want to have lightning indicators appear on the player's wrists when in Quicksilver.", 
            defaultValueIndex = 0, 
            order = 2)]
        public static bool useLightningIndicators;

        [ModOptionSlider]
        [ModOption(
            name: "Movement Speed", 
            tooltip: "Determines how fast the player moves when in Quicksilver.", 
            valueSourceName = nameof(ZeroToOneHundered), 
            defaultValueIndex = 22, 
            order = 3)]
        public static float movementSpeed;

        [ModOption(
            name: "Use Haptics", 
            tooltip: "Determines if you want to feel haptic feedback through your controllers when in Quicksilver.", 
            defaultValueIndex = 1, 
            order = 4)]
        public static bool useHaptics;

        [ModOption(
            name: "Use Custom Timescale", 
            tooltip: "Determines if the player wants to use a separate time scale from the in-game one for Quicksilver.", 
            category = "Custom Timescale", 
            defaultValueIndex = 0, 
            order = 0)]
        public static bool useCustomTimescale;

        [ModOptionSlider]
        [ModOption(
            name: "Custom Timescale Percentage", 
            tooltip: "Determines the player's time scale for Quicksilver if they decide to use the custom timescale option.", 
            category = "Custom Timescale", 
            valueSourceName = nameof(CustomTimescaleModOptions), 
            defaultValueIndex = 54, 
            order = 1)]
        public static float customTimescale;

        [ModOption(
            name: "Background Music", 
            tooltip: "Determines what music to play in the background when in Quicksilver, if any.", 
            category = "Music", 
            defaultValueIndex = 0, 
            order = 0)]
        public static void ChangeMusicChoice(QuicksilverMusic musicChoice)
        {
            bool playAfterChange = musicSource.isPlaying;

            switch (musicChoice)
            {
                case QuicksilverMusic.None:
                    musicSource.clip = musicClips[0];
                    break;
                case QuicksilverMusic.SweetDreams:
                    musicSource.clip = musicClips[1];
                    break;
                case QuicksilverMusic.TimeInABottle:
                    musicSource.clip = musicClips[2];
                    break;
                case QuicksilverMusic.Custom:
                    musicSource.clip = musicClips[3];
                    break;
            }

            musicTime = 0f;

            if (playAfterChange)
                musicSource.Play();
        }

        [ModOptionSlider]
        [ModOption(
            name: "Music Volume", 
            tooltip: "Determines the volume of the background music.", 
            valueSourceName = nameof(ZeroToOneHundered),
            category = "Music", 
            defaultValueIndex = 100, 
            order = 1)]
        public static void ChangeMusicVolume(float musicVolume)
        {
            musicSource.volume = musicVolume / 100f;
        }

        [ModOption(
            name: "Pause Music", 
            tooltip: "Determines whether to pause/resume music at the same spot on successive Quicksilvers instead of restarting it.", 
            category = "Music", 
            defaultValueIndex = 0, 
            order = 2)]
        public static bool pauseMusic;
       
        // SAVED DATA
        private static bool inQuicksilver;
        private EffectInstance leftInstance, rightInstance;
        private float quicksilverScale;

        private static AudioSource musicSource;
        private static AudioClip[] musicClips = new AudioClip[4];
        private static float musicTime;

        public struct OriginalSettings
        {
            public bool playerFallDamage;
            public bool haptics;
            public float? timeScale;
            public Locomotion.CrouchMode crouchMode;
        }
        public static OriginalSettings originalSettings;

        public struct OriginalData
        {
            public float fingerSpeed;
            public float animatorSpeed;
            public float horizontalAirSpeed;
            public float verticalAirSpeed;
            public float jumpGroundForce;
            public float jumpMaxDuration;
            public float creatureTurnSpeed;
            public float locomotionTurnSpeed;
            public float snapTurnDelay;
        }
        public static OriginalData originalData;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            new Harmony("Use").PatchAll();
            musicSource = GameManager.local.gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            GameManager.local.StartCoroutine(LoadMP3s());
        }

        [HarmonyPatch(typeof(SpellPowerSlowTime), "Use")]
        class SpellPowerSlowTimePatch
        {
            public static bool Prefix(bool active)
            {
                if ((PlayerControl.handRight.usePressed || PlayerControl.handLeft.usePressed) && useCustomTimescale && useTimeLord && !inQuicksilver && active)
                {
                    originalSettings.timeScale = Player.currentCreature.mana.GetPowerSlowTime().scale;
                    Player.currentCreature.mana.GetPowerSlowTime().scale = customTimescale;
                }
                return true;
            }
        }

        public override void ScriptUpdate()
        {
            base.ScriptUpdate();

            if (Player.currentCreature == null) return;

            switch (TimeManager.slowMotionState)
            {
                case TimeManager.SlowMotionState.Disabled:
                    if (inQuicksilver)
                        StopQuicksilver();
                    break;
                case TimeManager.SlowMotionState.Starting:
                    if (useTimeLord && !inQuicksilver && (PlayerControl.handRight.usePressed || PlayerControl.handLeft.usePressed))
                        StartQuicksilver();
                    break;
                case TimeManager.SlowMotionState.Stopping:
                    if (useInstantStop)
                        TimeManager.StopSlowMotion();
                    if (inQuicksilver)
                        StopQuicksilver();
                    break;
            }

            if (inQuicksilver)
            {
                Player.local.locomotion.physicBody.velocity = new Vector3(
                    Player.local.locomotion.moveDirection.x / Time.timeScale * movementSpeed,
                    Player.local.locomotion.physicBody.velocity.y,
                    Player.local.locomotion.moveDirection.z / Time.timeScale * movementSpeed);

                UpdateJoints(true);
            }
        }

        private void StartQuicksilver()
        {
            // Indicate that the player is in Quicksilver mode
            inQuicksilver = true;

            // Gather original settings
            originalSettings.playerFallDamage = Player.fallDamage;
            originalSettings.crouchMode = GameManager.options.stickCrouchMode;
            originalSettings.haptics = GameManager.options.rumble;

            // Set new settings for Quicksilver
            Player.fallDamage = false;
            GameManager.options.stickCrouchMode = Locomotion.CrouchMode.Disabled;
            GameManager.options.rumble = useHaptics;

            
            originalData.fingerSpeed = Player.currentCreature.data.ragdollData.fingerSpeed;
            originalData.animatorSpeed = Player.currentCreature.animator.speed;
            originalData.horizontalAirSpeed = Player.local.locomotion.horizontalAirSpeed;
            originalData.jumpGroundForce = Player.local.locomotion.jumpGroundForce;
            originalData.jumpMaxDuration = Player.local.locomotion.jumpMaxDuration;
            originalData.creatureTurnSpeed = Player.currentCreature.turnSpeed;
            originalData.locomotionTurnSpeed = Player.local.locomotion.turnSpeed;
            originalData.snapTurnDelay = PlayerControl.local.snapTurnDelay;

            // Disable camera effects
            CameraEffects.local.enabled = false;

            // Check if they are using lightning indicators
            if (useLightningIndicators)
            {
                EffectData lightningEffect = Catalog.GetData<EffectData>("ImbueLightningRagdoll");
                lightningEffect.volumeDb = float.MinValue;

                leftInstance = lightningEffect.Spawn(Player.local.handLeft.ragdollHand.transform);
                leftInstance.Play();

                rightInstance = lightningEffect.Spawn(Player.local.handRight.ragdollHand.transform);
                rightInstance.Play();
            }

            // Set the time in the song depending if the user is using pauseMusic
            if (pauseMusic)
            {
                musicSource.time = musicTime;
            }
            else
            {
                musicSource.time = 0f;
            }

            musicSource.Play();

            Player.local.locomotion.customGravity = Mathf.Pow(Time.timeScale, -2);
            quicksilverScale = Time.timeScale;

            UpdateQuicksilver(true);
        }

        private void UpdateQuicksilver(bool entering)
        {
            // SPEED MODIFIERS
            Player.currentCreature.data.ragdollData.fingerSpeed = originalData.fingerSpeed / (entering ? Time.timeScale : 1);
            Player.currentCreature.animator.speed = originalData.animatorSpeed / (entering ? Time.timeScale : 1);
            Player.local.locomotion.horizontalAirSpeed = entering ? Player.local.locomotion.forwardSpeed : originalData.horizontalAirSpeed;

            // JUMP FORCE MODIFIERS
            Player.local.locomotion.jumpGroundForce = originalData.jumpGroundForce / (entering ? Time.timeScale : 1);
           //  Player.local.locomotion.jumpMaxDuration = originalData.jumpMaxDuration * (entering ? Time.timeScale : 1);

            // TURN MODIFIERS
            Player.currentCreature.turnSpeed = originalData.creatureTurnSpeed / (entering ? Time.timeScale : 1);
            Player.local.locomotion.turnSpeed = originalData.locomotionTurnSpeed / (entering ? Time.timeScale : 1);
            PlayerControl.local.snapTurnDelay = originalData.snapTurnDelay * (entering ? Time.timeScale : 1);
        }

        private void StopQuicksilver()
        {
            // Indicate that the player is no longer in quicksilver mode
            inQuicksilver = false;

            // Check if they are using custom timescale. Reset scale as necessary.
            if (originalSettings.timeScale != null)
            {
                Player.currentCreature.mana.GetPowerSlowTime().scale = (float)originalSettings.timeScale;
                originalSettings.timeScale = null;
            }
                

            // Check if they are using lightning indicators. Stop as necessary.
            if (leftInstance != null || rightInstance != null)
            {
                leftInstance.Stop();
                rightInstance.Stop();
                leftInstance = null;
                rightInstance = null;
            }

            // Check if they are using background music. Stop as necessary.
            if (pauseMusic)
            {
                musicTime = musicSource.time;
            } 
            else
            {
                musicTime = 0f;
            }
            musicSource.Stop();

            // Restore all original value
            Player.fallDamage = originalSettings.playerFallDamage;
            GameManager.options.stickCrouchMode = originalSettings.crouchMode;
            GameManager.options.rumble = originalSettings.haptics;

            // Restore camera effects
            CameraEffects.local.enabled = true;

            // Reset the custom gravity felt by the player
            Player.local.locomotion.customGravity = 0f;

            // Stops the player from jumping and flying super high into the sky
            Player.local.locomotion.physicBody.velocity = new Vector3(
                Player.local.locomotion.moveDirection.x,
                Player.local.locomotion.physicBody.velocity.y * Mathf.Pow(quicksilverScale, 2),
                Player.local.locomotion.moveDirection.z);

            // Reset the player attributes to normal scale
            UpdateQuicksilver(false);

            UpdateJoints(false);
        }

        private void UpdateJoints(bool isEntering)
        {
            if (isEntering)
            {
                if (Player.currentCreature.handLeft.grabbedHandle)
                    Player.local.handLeft.ragdollHand.grabbedHandle.SetJointDrive(
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1));
                else
                    Player.local.handLeft.link.SetJointConfig(
                        Player.local.handLeft.link.controllerJoint,
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1),
                        Player.currentCreature.data.forceMaxPosition * 100 / Time.timeScale,
                        Player.currentCreature.data.forceMaxRotation * 100 / Time.timeScale);
                if (Player.currentCreature.handRight.grabbedHandle)
                    Player.local.handRight.ragdollHand.grabbedHandle.SetJointDrive(
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1));
                else
                    Player.local.handRight.link.SetJointConfig(
                        Player.local.handRight.link.controllerJoint,
                        new Vector2(100f / Time.timeScale, 1),
                        new Vector2(100f / Time.timeScale, 1),
                        Player.currentCreature.data.forceMaxPosition * 100 / Time.timeScale,
                        Player.currentCreature.data.forceMaxRotation * 100 / Time.timeScale);
            }
            else
            {
                if (Player.currentCreature.handLeft.grabbedHandle)
                    Player.local.handLeft.ragdollHand.grabbedHandle.RefreshAllJointDrives();
                else
                    Player.local.handLeft.link.RefreshJointConfig();
                if (Player.currentCreature.handRight.grabbedHandle)
                    Player.local.handRight.ragdollHand.grabbedHandle.RefreshAllJointDrives();
                else
                    Player.local.handRight.link.RefreshJointConfig();
            }
        }

        private IEnumerator LoadMP3s()
        {
            string[] mp3Files = Directory.GetFiles(Application.streamingAssetsPath + "/Mods/Quicksilver", "*.mp3", SearchOption.AllDirectories);

            musicClips[0] = AudioClip.Create("empty", 44100, 1, 44100, false);
            Catalog.LoadAssetAsync<AudioClip>("ChillioX.Quicksilver.SweetDreams", value => musicClips[1] = value, "ChillioX");
            Catalog.LoadAssetAsync<AudioClip>("ChillioX.Quicksilver.TimeInABottle", value => musicClips[2] = value, "ChillioX");

            if (mp3Files.Length == 0)
            {
                Debug.LogWarning("(Time Lord) Unable to find any MP3 clips!");
            }

            System.Random random = new System.Random();
            UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip("file://" + mp3Files[random.Next(0, mp3Files.Length)], AudioType.MPEG);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                musicClips[3] = DownloadHandlerAudioClip.GetContent(req);
            }
            else
            {
                Debug.LogError("(Time Lord) Unable to grab custom MP3 clip!");
            }
        }
    }
}