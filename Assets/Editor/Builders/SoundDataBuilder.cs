using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 사운드 데이터 빌더 (S1)
    /// - Assets/Audio/SFX/**/*.wav 임포트 설정
    /// - SoundId 별 SoundEvent 에셋 (Assets/Resources/Data/Sound/Events/&lt;ID&gt;.asset)
    /// - SoundLibrary 에셋 (events, weapons. groupGains 는 기존 값 유지)
    /// 여러 번 돌려도 같은 결과.
    /// </summary>
    public static class SoundDataBuilder
    {
        public const string SFX_DIR = "Assets/Audio/SFX";
        public const string BGM_DIR = "Assets/Audio/BGM";
        public const string DATA_DIR = "Assets/Resources/Data/Sound";
        public const string EVENT_DIR = DATA_DIR + "/Events";
        public const string LIBRARY_PATH = DATA_DIR + "/SoundLibrary.asset";

        private static readonly Regex VARIANT = new Regex(@"^(.+?)_(\d{2})$");

        private struct EventSpec
        {
            public SoundGroup group;
            public float volume, pitchMin, pitchMax, minInterval;
            public int maxVoices, priority;
            public bool ignorePause, loop;

            public EventSpec(SoundGroup _g, float _v, float _pMin, float _pMax, float _interval, int _voices, bool _ignorePause, int _priority, bool _loop = false)
            {
                group = _g; volume = _v; pitchMin = _pMin; pitchMax = _pMax; minInterval = _interval;
                maxVoices = _voices; ignorePause = _ignorePause; priority = _priority; loop = _loop;
            }
        }

        private static bool TryGetSpec(SoundId _id, out EventSpec _spec)
        {
            switch (_id)
            {
                case SoundId.UI_CLICK:
                case SoundId.LVL_SELECT:
                    _spec = new EventSpec(SoundGroup.UI, 1f, 0.97f, 1.03f, 0.03f, 2, true, 40); return true;
                case SoundId.LVL_UP:
                    _spec = new EventSpec(SoundGroup.Reward, 1f, 1f, 1f, 0.2f, 1, true, 40); return true;
                case SoundId.PLR_HIT:
                    _spec = new EventSpec(SoundGroup.Alert, 1f, 0.95f, 1.05f, 0.15f, 1, false, 10); return true;
                case SoundId.PLR_DEATH:
                    _spec = new EventSpec(SoundGroup.Alert, 1f, 1f, 1f, 0.5f, 1, true, 10); return true;
                case SoundId.EXP_PICKUP:
                    _spec = new EventSpec(SoundGroup.Reward, 0.8f, 1f, 1f, 0.05f, 2, false, 40); return true;
                case SoundId.MON_DIE_S:
                    _spec = new EventSpec(SoundGroup.Monster, 1f, 0.93f, 1.07f, 0.06f, 3, false, 180); return true;

                // S2
                case SoundId.UI_BACK:
                case SoundId.UI_DENIED:
                case SoundId.UI_START:
                case SoundId.DIM_CANCEL:
                case SoundId.DIM_CONFIRM_FOREST:
                case SoundId.DIM_CONFIRM_FACTORY:
                case SoundId.DIM_CONFIRM_STORM:
                case SoundId.DIM_READY:
                    _spec = new EventSpec(SoundGroup.UI, 1f, 1f, 1f, 0.03f, 2, true, 40); return true;
                case SoundId.DIM_CURSOR:
                    _spec = new EventSpec(SoundGroup.UI, 1f, 0.97f, 1.03f, 0.03f, 2, true, 40); return true;
                case SoundId.UI_SLIDER:
                    _spec = new EventSpec(SoundGroup.UI, 1f, 1f, 1f, 0.08f, 1, true, 40); return true;
                case SoundId.PAUSE_OPEN:
                case SoundId.PAUSE_CLOSE:
                    _spec = new EventSpec(SoundGroup.UI, 1f, 1f, 1f, 0.05f, 1, true, 40); return true;
                case SoundId.LVL_WEAPON:
                case SoundId.LVL_UPGRADE:
                case SoundId.LVL_STAR:
                case SoundId.LVL_BUFF:
                    _spec = new EventSpec(SoundGroup.Reward, 1f, 1f, 1f, 0.05f, 2, true, 40); return true;
                case SoundId.LVL_DEBUFF:
                    _spec = new EventSpec(SoundGroup.Reward, 0.7f, 1f, 1f, 0.05f, 2, true, 40); return true;
                case SoundId.RESULT_WIN:
                case SoundId.RESULT_LOSE:
                    _spec = new EventSpec(SoundGroup.Reward, 1f, 1f, 1f, 1f, 1, true, 20); return true;
                // WAVE_START / WAVE_RUSH / WAVE_FINAL : 웨이브 전환은 무음으로 하기로 함 (2026-11-10). 음원·이벤트를 만들지 않는다
                case SoundId.PLR_LOW_HP:
                    _spec = new EventSpec(SoundGroup.Alert, 0.8f, 1f, 1f, 0f, 1, false, 15, true); return true;
                case SoundId.WPN_SPINBLADE_TELL:
                    _spec = new EventSpec(SoundGroup.Weapon, 1f, 1f, 1f, 0.1f, 1, false, 128); return true;
                case SoundId.WPN_MINE_BOOM:
                case SoundId.WPN_EXPLOSIVE_BOOM:
                case SoundId.WPN_LIGHTNING_STRIKE:
                case SoundId.WPN_SPLIT_BURST:
                    _spec = new EventSpec(SoundGroup.Weapon, 1f, 0.94f, 1.06f, 0.1f, 3, false, 128); return true;
                case SoundId.MON_DIE_M:
                case SoundId.MON_DIE_L:
                    _spec = new EventSpec(SoundGroup.Monster, 1f, 0.93f, 1.07f, 0.08f, 2, false, 180); return true;
                case SoundId.MON_BOMBER_TELL:
                    _spec = new EventSpec(SoundGroup.Alert, 0.8f, 1f, 1f, 0.12f, 2, false, 30); return true;
                case SoundId.MON_BOMBER_BOOM:
                    _spec = new EventSpec(SoundGroup.Alert, 1f, 0.95f, 1.05f, 0.1f, 3, false, 30); return true;
                case SoundId.MON_TELEPORT:
                case SoundId.MON_SUMMON:
                case SoundId.MON_REVIVE:
                    _spec = new EventSpec(SoundGroup.Monster, 1f, 0.95f, 1.05f, 0.12f, 2, false, 170); return true;
            }
            string n = _id.ToString();
            if (n.StartsWith("WPN_", StringComparison.Ordinal) && n.EndsWith("_LOOP", StringComparison.Ordinal))
            {
                _spec = new EventSpec(SoundGroup.Weapon, 0.6f, 1f, 1f, 0f, 1, false, 128, true); return true;
            }
            if (n.StartsWith("WPN_", StringComparison.Ordinal))
            {
                _spec = new EventSpec(SoundGroup.Weapon, 1f, 0.94f, 1.06f, 0.06f, 2, false, 128); return true;
            }
            if (n.StartsWith("BGM_", StringComparison.Ordinal))
            {
                _spec = new EventSpec(SoundGroup.BGM, 1f, 1f, 1f, 0f, 1, true, 0, true); return true;
            }
            if (n.StartsWith("HIT_", StringComparison.Ordinal))
            {
                _spec = new EventSpec(SoundGroup.Hit, 1f, 0.92f, 1.08f, 0.045f, 3, false, 200); return true;
            }
            _spec = default;
            return false;
        }

        // weaponKey → (fire, hit, loop). "gun" 은 DroneProjectile 프리팹 안 자식 Weapon 의 실제 이름
        private static readonly (string key, SoundId fire, SoundId hit, SoundId loop)[] WEAPONS =
        {
            ("Boomerang", SoundId.WPN_BOOMERANG, SoundId.HIT_ORGANIC, SoundId.None),
            ("PoisonZone", SoundId.WPN_POISON_THROW, SoundId.HIT_ORGANIC, SoundId.None),
            ("Sweep", SoundId.WPN_SWEEP, SoundId.HIT_ORGANIC, SoundId.None),
            ("SplitShot", SoundId.WPN_SPLIT_FIRE, SoundId.HIT_ORGANIC, SoundId.None),
            ("Vine", SoundId.None, SoundId.HIT_ORGANIC, SoundId.None),
            ("Spirit", SoundId.WPN_SPIRIT_SPAWN, SoundId.HIT_ORGANIC, SoundId.None),
            ("Root", SoundId.WPN_ROOT, SoundId.HIT_ORGANIC, SoundId.None),
            ("Laser", SoundId.WPN_LASER, SoundId.HIT_METAL, SoundId.None),
            ("Drone", SoundId.None, SoundId.HIT_METAL, SoundId.None),
            ("gun", SoundId.WPN_DRONE, SoundId.HIT_METAL, SoundId.None),
            ("Mine", SoundId.WPN_MINE_SET, SoundId.HIT_METAL, SoundId.None),
            ("Saw", SoundId.None, SoundId.HIT_METAL, SoundId.WPN_SAW_LOOP),
            ("Flame", SoundId.None, SoundId.HIT_METAL, SoundId.WPN_FLAME_LOOP),
            ("SpinBlade", SoundId.None, SoundId.HIT_METAL, SoundId.WPN_SPINBLADE_LOOP),
            ("Chain", SoundId.WPN_CHAIN, SoundId.HIT_SHOCK, SoundId.None),
            ("Shockwave", SoundId.WPN_SHOCKWAVE, SoundId.HIT_SHOCK, SoundId.None),
            ("Explosive", SoundId.WPN_EXPLOSIVE_FIRE, SoundId.HIT_SHOCK, SoundId.None),
            ("Tornado", SoundId.None, SoundId.HIT_SHOCK, SoundId.WPN_TORNADO_LOOP),
            ("Lightning", SoundId.WPN_LIGHTNING_MARK, SoundId.HIT_SHOCK, SoundId.None),
            ("StormOrb", SoundId.None, SoundId.HIT_SHOCK, SoundId.WPN_STORMORB_LOOP),
        };

        // MonsterData 에셋 이름 → 사망음
        private static readonly (string key, SoundId die)[] MONSTERS =
        {
            ("BomberData", SoundId.MON_DIE_M),
            ("WraithData", SoundId.MON_DIE_M),
            ("ShamanData", SoundId.MON_DIE_M),
            ("GolemData", SoundId.MON_DIE_L),
            ("SlimeData", SoundId.MON_DIE_S),
            ("MiniSlimeData", SoundId.MON_DIE_S),
        };

        // EffectPool 로 꺼내는 이펙트 프리팹 이름 → 소리
        private static readonly (string key, SoundId sound)[] EFFECTS =
        {
            ("MineExplosion", SoundId.WPN_MINE_BOOM),
            ("ExplosiveExplosionEffect", SoundId.WPN_EXPLOSIVE_BOOM),
            ("SplitShotBurst", SoundId.WPN_SPLIT_BURST),
            ("BomberExplosion", SoundId.MON_BOMBER_BOOM),
        };

        // DimensionData 에셋 이름 → 확정음
        private static readonly (string key, SoundId confirm, SoundId layer)[] DIMENSIONS =
        {
            ("Forest", SoundId.DIM_CONFIRM_FOREST, SoundId.BGM_LAYER_FOREST),
            ("Factory", SoundId.DIM_CONFIRM_FACTORY, SoundId.BGM_LAYER_FACTORY),
            ("Storm", SoundId.DIM_CONFIRM_STORM, SoundId.BGM_LAYER_STORM),
        };

        // 씬 이름 → BGM 방식
        private static readonly (string scene, BgmMode mode, SoundId single)[] BGM_SCENES =
        {
            ("TitleScene", BgmMode.Single, SoundId.BGM_TITLE),
            ("DimensionSelectScene", BgmMode.Single, SoundId.BGM_SELECT),
            ("GameScene", BgmMode.Combat, SoundId.None),
        };

        // 원샷 16 + 루프 6 + BGM 8 = 30 (Real Voices 32 이내)
        private const int LOOP_VOICES = 6;
        private const float BGM_QUALITY = 0.6f;

        private const SoundId DEFAULT_MONSTER_DIE = SoundId.MON_DIE_S;
        private const int RUSH_WAVE_INDEX = 10;
        private const float LOW_HP_RATIO = 0.25f;
        private static readonly int[] STAR_UPGRADE_LEVELS = { 5, 10 };
        private const float CULL_VIEWPORT_MARGIN = 0.1f;
        private const float CAMERA_RETRY_INTERVAL = 1f;

        [MenuItem("Tools/Builders/Sound Data")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var warnings = new List<string>();

            // 1. 임포트 설정
            string[] wavs = new[] { SFX_DIR, BGM_DIR }
                .Where(d => Directory.Exists(d))
                .SelectMany(d => Directory.GetFiles(d, "*.wav", SearchOption.AllDirectories))
                .Select(p => p.Replace('\\', '/')).OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray();
            int reimported = 0;
            foreach (string path in wavs)
            {
                if (true == ApplyImport(path))
                {
                    ++reimported;
                }
            }

            // 2. 파일 → ID 묶기
            var byId = new Dictionary<SoundId, List<string>>();
            foreach (string path in wavs)
            {
                string file = Path.GetFileNameWithoutExtension(path);
                string idName = file;
                Match m = VARIANT.Match(file);
                if (true == m.Success && Enum.IsDefined(typeof(SoundId), m.Groups[1].Value))
                {
                    idName = m.Groups[1].Value;
                }
                if (false == Enum.TryParse(idName, false, out SoundId id) || false == Enum.IsDefined(typeof(SoundId), idName) || SoundId.None == id)
                {
                    warnings.Add($"SoundId 없음: {file}");
                    continue;
                }
                if (false == byId.TryGetValue(id, out var list))
                {
                    list = new List<string>();
                    byId.Add(id, list);
                }
                list.Add(path);
            }

            // 3. SoundEvent 에셋
            EnsureFolder(EVENT_DIR);
            var events = new List<SoundEvent>();
            foreach (SoundId id in byId.Keys.OrderBy(i => (int)i))
            {
                if (false == TryGetSpec(id, out EventSpec spec))
                {
                    warnings.Add($"설정 표에 없는 ID: {id}");
                    continue;
                }
                List<string> files = byId[id].OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToList();
                events.Add(BuildEvent(id, spec, files));
            }

            // 4. SoundLibrary
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LIBRARY_PATH);
            if (null == library)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LIBRARY_PATH);
            }
            var so = new SerializedObject(library);
            SerializedProperty evProp = so.FindProperty("events");
            evProp.arraySize = events.Count;
            for (int i = 0; i < events.Count; ++i)
            {
                evProp.GetArrayElementAtIndex(i).objectReferenceValue = events[i];
            }
            SerializedProperty wProp = so.FindProperty("weapons");
            wProp.arraySize = WEAPONS.Length;
            for (int i = 0; i < WEAPONS.Length; ++i)
            {
                SerializedProperty e = wProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("weaponKey").stringValue = WEAPONS[i].key;
                e.FindPropertyRelative("fire").intValue = (int)WEAPONS[i].fire;
                e.FindPropertyRelative("hit").intValue = (int)WEAPONS[i].hit;
                e.FindPropertyRelative("loop").intValue = (int)WEAPONS[i].loop;
                CheckSound(byId, WEAPONS[i].key + " loop", WEAPONS[i].loop, warnings);
                if (SoundId.None != WEAPONS[i].fire && false == byId.ContainsKey(WEAPONS[i].fire))
                {
                    warnings.Add($"{WEAPONS[i].key} fire 음원 없음: {WEAPONS[i].fire}");
                }
                if (SoundId.None != WEAPONS[i].hit && false == byId.ContainsKey(WEAPONS[i].hit))
                {
                    warnings.Add($"{WEAPONS[i].key} hit 음원 없음: {WEAPONS[i].hit}");
                }
            }
            SerializedProperty mProp = so.FindProperty("monsters");
            mProp.arraySize = MONSTERS.Length;
            for (int i = 0; i < MONSTERS.Length; ++i)
            {
                SerializedProperty e = mProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("monsterKey").stringValue = MONSTERS[i].key;
                e.FindPropertyRelative("die").intValue = (int)MONSTERS[i].die;
                CheckSound(byId, MONSTERS[i].key, MONSTERS[i].die, warnings);
                if (false == AssetExists(MONSTERS[i].key, "t:MonsterData"))
                {
                    warnings.Add($"MonsterData 에셋 없음: {MONSTERS[i].key}");
                }
            }
            SerializedProperty fxProp = so.FindProperty("effects");
            fxProp.arraySize = EFFECTS.Length;
            for (int i = 0; i < EFFECTS.Length; ++i)
            {
                SerializedProperty e = fxProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("effectKey").stringValue = EFFECTS[i].key;
                e.FindPropertyRelative("sound").intValue = (int)EFFECTS[i].sound;
                CheckSound(byId, EFFECTS[i].key, EFFECTS[i].sound, warnings);
                if (false == AssetExists(EFFECTS[i].key, "t:Prefab"))
                {
                    warnings.Add($"이펙트 프리팹 없음: {EFFECTS[i].key}");
                }
            }
            SerializedProperty dProp = so.FindProperty("dimensions");
            dProp.arraySize = DIMENSIONS.Length;
            for (int i = 0; i < DIMENSIONS.Length; ++i)
            {
                SerializedProperty e = dProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("dimensionKey").stringValue = DIMENSIONS[i].key;
                e.FindPropertyRelative("confirm").intValue = (int)DIMENSIONS[i].confirm;
                e.FindPropertyRelative("layer").intValue = (int)DIMENSIONS[i].layer;
                CheckSound(byId, DIMENSIONS[i].key + " layer", DIMENSIONS[i].layer, warnings);
                CheckSound(byId, DIMENSIONS[i].key, DIMENSIONS[i].confirm, warnings);
                if (false == AssetExists(DIMENSIONS[i].key, "t:DimensionData"))
                {
                    warnings.Add($"DimensionData 에셋 없음: {DIMENSIONS[i].key}");
                }
            }
            so.FindProperty("defaultMonsterDie").intValue = (int)DEFAULT_MONSTER_DIE;
            so.FindProperty("rushWaveIndex").intValue = RUSH_WAVE_INDEX;
            so.FindProperty("lowHpRatio").floatValue = LOW_HP_RATIO;
            SerializedProperty starProp = so.FindProperty("starUpgradeLevels");
            starProp.arraySize = STAR_UPGRADE_LEVELS.Length;
            for (int i = 0; i < STAR_UPGRADE_LEVELS.Length; ++i)
            {
                starProp.GetArrayElementAtIndex(i).intValue = STAR_UPGRADE_LEVELS[i];
            }
            so.FindProperty("cullViewportMargin").floatValue = CULL_VIEWPORT_MARGIN;
            so.FindProperty("cameraRetryInterval").floatValue = CAMERA_RETRY_INTERVAL;
            so.FindProperty("loopVoices").intValue = LOOP_VOICES;
            SerializedProperty bgmProp = so.FindProperty("bgmScenes");
            bgmProp.arraySize = BGM_SCENES.Length;
            for (int i = 0; i < BGM_SCENES.Length; ++i)
            {
                SerializedProperty e = bgmProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("sceneName").stringValue = BGM_SCENES[i].scene;
                e.FindPropertyRelative("mode").intValue = (int)BGM_SCENES[i].mode;
                e.FindPropertyRelative("single").intValue = (int)BGM_SCENES[i].single;
                CheckSound(byId, BGM_SCENES[i].scene, BGM_SCENES[i].single, warnings);
                if (false == File.Exists($"Assets/Scenes/{BGM_SCENES[i].scene}.unity"))
                {
                    warnings.Add($"씬 없음: {BGM_SCENES[i].scene}");
                }
            }
            // Combat 이 쓰는 베이스·러시도 음원이 있어야 한다
            foreach (SoundId bgm in new[] { SoundId.BGM_BASE_LOW, SoundId.BGM_BASE_MID, SoundId.BGM_BASE_HIGH, SoundId.BGM_RUSH })
            {
                CheckSound(byId, "Combat", bgm, warnings);
            }
            // groupGains 는 건드리지 않는다 (새 에셋이면 코드 기본값)
            so.ApplyModifiedPropertiesWithoutUndo();
            library.InvalidateCache();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            foreach (string w in warnings)
            {
                Debug.LogWarning("[SoundDataBuilder] " + w);
            }
            return $"SoundDataBuilder: wav {wavs.Length}개 (재임포트 {reimported}), 이벤트 {events.Count}개, 무기 {WEAPONS.Length} / 몬스터 {MONSTERS.Length} / 이펙트 {EFFECTS.Length} / 차원 {DIMENSIONS.Length}, 경고 {warnings.Count}개"
                + (warnings.Count > 0 ? " [" + string.Join(" / ", warnings) + "]" : "");
        }

        /// <summary>
        /// 임포트 설정을 맞춘다. 바뀌었으면 재임포트하고 true
        /// </summary>
        private static bool ApplyImport(string _path)
        {
            var importer = AssetImporter.GetAtPath(_path) as AudioImporter;
            if (null == importer)
            {
                return false;
            }
            bool changed = false;
            if (false == importer.forceToMono)
            {
                importer.forceToMono = true;
                changed = true;
            }
            // 모노 다운믹스가 길이에 영향을 주지 않으므로 먼저 길이를 본다
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_path);
            float length = null != clip ? clip.length : 0f;

            AudioImporterSampleSettings s = importer.defaultSampleSettings;

            // BGM : 레이어 샘플 동기를 위해 Streaming 금지. 전부 메모리에 압축 상태로 올린다
            if (true == _path.StartsWith(BGM_DIR + "/", StringComparison.Ordinal))
            {
                if (true == importer.loadInBackground)
                {
                    importer.loadInBackground = false;
                    changed = true;
                }
                if (AudioClipLoadType.CompressedInMemory != s.loadType || AudioCompressionFormat.Vorbis != s.compressionFormat
                    || false == s.preloadAudioData || false == Mathf.Approximately(s.quality, BGM_QUALITY))
                {
                    s.loadType = AudioClipLoadType.CompressedInMemory;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = BGM_QUALITY;
                    s.preloadAudioData = true;
                    importer.defaultSampleSettings = s;
                    changed = true;
                }
                if (true == changed)
                {
                    importer.SaveAndReimport();
                }
                return changed;
            }

            // 루프 파일은 길이와 상관없이 ADPCM (Vorbis 는 루프 이음새에 틈이 생길 수 있다)
            bool shortOrLoop = length < 1f || true == IsLoopFile(_path);
            AudioClipLoadType loadType = shortOrLoop ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            AudioCompressionFormat format = shortOrLoop ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
            if (s.loadType != loadType || s.compressionFormat != format || false == s.preloadAudioData
                || (AudioCompressionFormat.Vorbis == format && false == Mathf.Approximately(s.quality, 0.7f)))
            {
                s.loadType = loadType;
                s.compressionFormat = format;
                s.preloadAudioData = true;
                if (AudioCompressionFormat.Vorbis == format)
                {
                    s.quality = 0.7f;
                }
                importer.defaultSampleSettings = s;
                changed = true;
            }
            if (true == changed)
            {
                importer.SaveAndReimport();
            }
            return changed;
        }

        private static bool IsLoopFile(string _path)
        {
            string file = Path.GetFileNameWithoutExtension(_path);
            Match m = VARIANT.Match(file);
            if (true == m.Success)
            {
                file = m.Groups[1].Value;
            }
            return file.EndsWith("_LOOP", StringComparison.Ordinal) || file == nameof(SoundId.PLR_LOW_HP);
        }

        private static void CheckSound(Dictionary<SoundId, List<string>> _byId, string _owner, SoundId _id, List<string> _warnings)
        {
            if (SoundId.None != _id && false == _byId.ContainsKey(_id))
            {
                _warnings.Add($"{_owner} 음원 없음: {_id}");
            }
        }

        private static bool AssetExists(string _name, string _filter)
        {
            foreach (string guid in AssetDatabase.FindAssets(_name + " " + _filter))
            {
                if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == _name)
                {
                    return true;
                }
            }
            return false;
        }

        private static SoundEvent BuildEvent(SoundId _id, EventSpec _spec, List<string> _files)
        {
            string path = $"{EVENT_DIR}/{_id}.asset";
            SoundEvent ev = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
            if (null == ev)
            {
                ev = ScriptableObject.CreateInstance<SoundEvent>();
                AssetDatabase.CreateAsset(ev, path);
            }
            var so = new SerializedObject(ev);
            so.FindProperty("id").intValue = (int)_id;
            SerializedProperty clips = so.FindProperty("clips");
            clips.arraySize = _files.Count;
            for (int i = 0; i < _files.Count; ++i)
            {
                clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(_files[i]);
            }
            so.FindProperty("group").intValue = (int)_spec.group;
            so.FindProperty("volume").floatValue = _spec.volume;
            so.FindProperty("pitchMin").floatValue = _spec.pitchMin;
            so.FindProperty("pitchMax").floatValue = _spec.pitchMax;
            so.FindProperty("minInterval").floatValue = _spec.minInterval;
            so.FindProperty("maxVoices").intValue = _spec.maxVoices;
            so.FindProperty("ignorePause").boolValue = _spec.ignorePause;
            so.FindProperty("priority").intValue = _spec.priority;
            so.FindProperty("loop").boolValue = _spec.loop;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ev);
            return ev;
        }

        private static void EnsureFolder(string _path)
        {
            if (true == AssetDatabase.IsValidFolder(_path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
        }
    }
}
