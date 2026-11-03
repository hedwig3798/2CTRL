using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// 픽셀 아트 리소스 적용 (멱등). 원본 PNG 는 tools/art/*.py 로 만든다.
    /// 1. Textuer/Art 임포트 설정 (Point, 무압축, PPU 32, 그리드 분할)
    /// 2. 플레이어·몬스터 애니메이션 클립 / 컨트롤러 / 오버라이드
    /// 3. 차원별 WFC 타일 프리팹·청크 로더
    /// 4. 무기·이펙트·아이템 스프라이트, 보상 아이콘
    /// 5. UI (게이지, 패널, 버튼, 폰트) 와 타이틀·차원 선택·게임 오버 화면
    /// </summary>
    public static class ArtResourceBuilder
    {
        private const string ART = "Assets/Resources/Textuer/Art";
        private const string ANIM = "Assets/prefab/Art/Anim";
        private const string MAT = "Assets/prefab/Art/Material";
        private const string TILE_DIR = "Assets/prefab/TileMap/Art";
        private const string SOCKET_DIR = "Assets/prefab/TileMap/Sokets";
        private const string MONSTER_DIR = "Assets/prefab/Monster";
        private const string MONSTER_DATA = "Assets/Resources/Data/Monster";
        private const string DIM_DATA = "Assets/Resources/Data/Dimension";
        private const string FONT_TTF = "Assets/Fonts/Galmuri/Galmuri11.ttf";
        private const string FONT_ASSET = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        private const string UNLIT = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const int PPU = 32;
        private const float ANIM_FPS = 8f;

        // UI 픽셀을 화면에서 2배로 보이게 (Canvas 기준 PPU 100 / 스프라이트 PPU 32 / 2)
        private const float UI_PIXEL_MULT = 100f / PPU / 2f;

        // tools/art/tiles.py 의 SHAPES 순서와 같아야 한다
        private static readonly string[] TILE_SHAPES =
        {
            "A0", "A1", "A2", "A3", "B0", "B1", "B2",
            "OuterTL", "EdgeT", "OuterTR", "EdgeL", "EdgeR", "OuterBL", "EdgeB", "OuterBR",
            "InnerTL", "InnerTR", "InnerBL", "InnerBR",
        };

        private static readonly StringBuilder log = new StringBuilder();

        public static string Build()
        {
            log.Clear();
            AssetDatabase.StartAssetEditing();
            try
            {
                ImportAll();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();

            EnsureFolder(ANIM + "/Override");
            EnsureFolder(MAT);

            BuildPlayers();
            BuildMonsters();
            BuildTiles();
            BuildWeapons();
            BuildExtraWeapons();
            BuildItems();
            BuildIcons();
            TMP_FontAsset font = BuildFont();
            BuildPrefabUI(font);
            BuildScenes(font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        #region Import

        private static void ImportAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ART }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (null == ti)
                {
                    continue;
                }

                ti.textureType = TextureImporterType.Sprite;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = PPU;
                ti.wrapMode = path.EndsWith("_Chain_Line.png") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool strip = path.Contains("/Player/") || path.Contains("/Monster/");
                bool tiles = path.Contains("/TileMap/");

                if (strip || tiles)
                {
                    ti.spriteImportMode = SpriteImportMode.Multiple;
                    ti.SaveAndReimport();
                    SliceGrid(ti, file, tiles ? TILE_SHAPES : null);
                }
                else
                {
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.spritePivot = new Vector2(0.5f, 0.5f);
                    ti.spriteBorder = BorderFor(file);
                    ti.SaveAndReimport();
                }
            }
            log.AppendLine("import ok");
        }

        // 9-slice 테두리 (왼, 아래, 오른, 위)
        private static Vector4 BorderFor(string _file)
        {
            switch (_file)
            {
                case "Common_UI_Panel":
                case "Common_UI_Panel2":
                case "Common_UI_PanelDark":
                    return new Vector4(6, 5, 5, 6);
                case "Common_UI_PanelFocus":
                    return new Vector4(3, 3, 3, 3);
                case "Common_UI_Button_Normal":
                case "Common_UI_Button_Hover":
                case "Common_UI_Button_Pressed":
                    return new Vector4(4, 3, 4, 3);
                case "Common_UI_Bar_Frame":
                    return new Vector4(3, 3, 3, 3);
                case "Common_UI_Bar_HP_Fill":
                case "Common_UI_Bar_EXP_Fill":
                case "Common_UI_Bar_MonsterHP_Fill":
                    return new Vector4(0, 1, 0, 1);
                default:
                    return Vector4.zero;
            }
        }

        // 32x32 그리드로 자른다. 이름은 _names 가 있으면 그것, 없으면 file_0, file_1 ...
        // 같은 이름의 기존 스프라이트 ID 를 유지해서 다시 실행해도 참조가 끊기지 않는다
        private static void SliceGrid(TextureImporter _ti, string _file, string[] _names)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(_ti);
            provider.InitSpriteEditorDataProvider();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(_ti.assetPath);
            int cols = tex.width / 32;
            int rows = tex.height / 32;
            int count = null != _names ? _names.Length : cols * rows;

            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            for (int i = 0; i < count; ++i)
            {
                int c = i % cols;
                int r = i / cols;
                string name = null != _names ? _file.Replace("_Sheet", "") + "_" + _names[i] : _file + "_" + i;
                var rect = new SpriteRect
                {
                    name = name,
                    rect = new Rect(c * 32, tex.height - (r + 1) * 32, 32, 32),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existing.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                };
                rects.Add(rect);
            }

            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            _ti.SaveAndReimport();
        }

        private static Sprite[] Frames(string _path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(_path).OfType<Sprite>()
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        }

        private static Sprite Single(string _relative)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ART + "/" + _relative + ".png");
            if (null == s)
            {
                log.AppendLine("missing sprite " + _relative);
            }
            return s;
        }

        #endregion

        #region Animation

        private static AnimationClip Clip(string _strip)
        {
            string src = ART + "/" + (_strip.Contains("_Player_") ? "Player/" : "Monster/") + _strip + ".png";
            Sprite[] frames = Frames(src);
            string path = ANIM + "/" + _strip + ".anim";

            var clip = new AnimationClip { frameRate = ANIM_FPS };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; ++i)
            {
                keys[i] = new ObjectReferenceKeyframe { time = i / ANIM_FPS, value = frames[i] };
            }
            // 마지막 프레임 길이를 유지하기 위한 끝 키
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / ANIM_FPS, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (null == old)
            {
                AssetDatabase.CreateAsset(clip, path);
                return clip;
            }
            EditorUtility.CopySerialized(clip, old);
            EditorUtility.SetDirty(old);
            return old;
        }

        // 상태 이름 → 클립. 첫 상태가 기본. bool 파라미터 IsMoving 으로 Idle/Walk 전환 (상태가 둘일 때)
        private static AnimatorController Controller(string _path, params (string state, AnimationClip clip)[] _states)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(_path);
            if (null == ctrl)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(_path);
            }

            if (false == ctrl.parameters.Any(p => "IsMoving" == p.name) && 1 < _states.Length)
            {
                ctrl.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            }

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState child in sm.states.ToArray())
            {
                sm.RemoveState(child.state);
            }

            var created = new List<AnimatorState>();
            foreach (var (state, clip) in _states)
            {
                AnimatorState s = sm.AddState(state);
                s.motion = clip;
                created.Add(s);
            }
            sm.defaultState = created[0];

            if (2 == created.Count)
            {
                AnimatorStateTransition toWalk = created[0].AddTransition(created[1]);
                toWalk.hasExitTime = false;
                toWalk.duration = 0f;
                toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
                AnimatorStateTransition toIdle = created[1].AddTransition(created[0]);
                toIdle.hasExitTime = false;
                toIdle.duration = 0f;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static AnimatorOverrideController Override(string _path, RuntimeAnimatorController _base, params (AnimationClip from, AnimationClip to)[] _clips)
        {
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(_path);
            if (null == aoc)
            {
                aoc = new AnimatorOverrideController();
                AssetDatabase.CreateAsset(aoc, _path);
            }
            aoc.runtimeAnimatorController = _base;
            aoc.ApplyOverrides(_clips.Select(c => new KeyValuePair<AnimationClip, AnimationClip>(c.from, c.to)).ToList());
            EditorUtility.SetDirty(aoc);
            return aoc;
        }

        #endregion

        #region Player

        private static readonly (string dim, string asset, string strip)[] PLAYERS =
        {
            ("D1", "Forest", "D1_Player_Hunter"),
            ("D2", "Factory", "D2_Player_RobotGrey"),
            ("D3", "Storm", "D3_Player_NinjaThunder"),
        };

        private static void BuildPlayers()
        {
            AnimationClip baseIdle = Clip(PLAYERS[0].strip + "_Idle");
            AnimationClip baseWalk = Clip(PLAYERS[0].strip + "_Walk");
            AnimatorController ctrl = Controller(ANIM + "/PlayerBase.controller", ("Idle", baseIdle), ("Walk", baseWalk));

            foreach (var (dim, asset, strip) in PLAYERS)
            {
                AnimationClip idle = Clip(strip + "_Idle");
                AnimationClip walk = Clip(strip + "_Walk");
                var dd = AssetDatabase.LoadAssetAtPath<DimensionData>(DIM_DATA + "/" + asset + ".asset");
                var so = new SerializedObject(dd);

                // 기존 오버라이드 자산(Player_Forest 등)이 있으면 그것을 고쳐 쓴다
                var current = so.FindProperty("playerAnimator").objectReferenceValue as AnimatorOverrideController;
                string path = null != current ? AssetDatabase.GetAssetPath(current) : ANIM + "/Override/" + dim + "_Player.overrideController";
                AnimatorOverrideController aoc = Override(path, ctrl, (baseIdle, idle), (baseWalk, walk));

                so.FindProperty("playerAnimator").objectReferenceValue = aoc;
                so.FindProperty("playerSprite").objectReferenceValue = Frames(ART + "/Player/" + strip + "_Idle.png")[0];
                so.FindProperty("previewSprite").objectReferenceValue = Single("Title/" + dim + "_UI_Preview");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditPrefab("Assets/prefab/Player/Player.prefab", root =>
            {
                Transform visual = root.transform.Find("visual");
                visual.localScale = Vector3.one;
                var sr = visual.GetComponent<SpriteRenderer>();
                sr.sprite = Frames(ART + "/Player/" + PLAYERS[0].strip + "_Idle.png")[0];
                visual.GetComponent<Animator>().runtimeAnimatorController = ctrl;
                var mover = visual.GetComponent<MoveAnimator>();
                if (null == mover)
                {
                    mover = visual.gameObject.AddComponent<MoveAnimator>();
                }
                SetProp(mover, "target", root.transform);

                foreach (ArrowMovemnt move in root.GetComponentsInChildren<ArrowMovemnt>(true))
                {
                    SetProp(move, "isDefaultLeft", true);
                }
                StyleWorldBar(root.GetComponentInChildren<SliderUI>(true));
            });
            log.AppendLine("players ok");
        }

        #endregion

        #region Monster

        // 몬스터 프리팹 → 기본 외형 strip
        private static readonly (string prefab, string strip)[] MONSTER_DEFAULT =
        {
            ("Slime", "D1_Monster_Slime"), ("MiniSlime", "D1_Monster_Slime"), ("Golem", "D1_Monster_Golem"),
            ("Bomber", "D2_Monster_Bomber"), ("Wraith", "D3_Monster_Wraith"), ("Shaman", "D3_Monster_Shaman"),
            ("bat", "Common_Monster_Bat"),
        };

        // 차원 → (MonsterData 이름, strip)
        private static readonly Dictionary<string, (string data, string strip)[]> SKINS = new Dictionary<string, (string, string)[]>
        {
            { "Forest", new[] { ("SlimeData", "D1_Monster_Slime"), ("MiniSlimeData", "D1_Monster_Slime"), ("GolemData", "D1_Monster_Golem") } },
            { "Factory", new[] { ("BomberData", "D2_Monster_Bomber"), ("GolemData", "D2_Monster_Golem"), ("SlimeData", "D2_Monster_Slime"), ("MiniSlimeData", "D2_Monster_Slime") } },
            { "Storm", new[] { ("WraithData", "D3_Monster_Wraith"), ("ShamanData", "D3_Monster_Shaman") } },
        };

        private static readonly Dictionary<string, AnimatorOverrideController> monsterOverrides = new Dictionary<string, AnimatorOverrideController>();

        private static AnimationClip monsterBaseClip;

        private static AnimatorOverrideController MonsterOverride(AnimatorController _base, string _strip)
        {
            if (false == monsterOverrides.TryGetValue(_strip, out AnimatorOverrideController aoc))
            {
                aoc = Override(ANIM + "/Override/" + _strip + ".overrideController", _base, (monsterBaseClip, Clip(_strip + "_Walk")));
                monsterOverrides[_strip] = aoc;
            }
            return aoc;
        }

        private static void BuildMonsters()
        {
            monsterOverrides.Clear();
            monsterBaseClip = Clip("D1_Monster_Slime_Walk");
            AnimatorController ctrl = Controller(ANIM + "/MonsterBase.controller", ("Walk", monsterBaseClip));

            foreach (var (prefab, strip) in MONSTER_DEFAULT)
            {
                AnimatorOverrideController aoc = MonsterOverride(ctrl, strip);
                EditPrefab(MONSTER_DIR + "/" + prefab + "/" + prefab + ".prefab", root =>
                {
                    Transform visual = root.transform.Find("visual");
                    var sr = visual.GetComponent<SpriteRenderer>();
                    sr.sprite = Frames(ART + "/Monster/" + strip + "_Walk.png")[0];
                    sr.color = Color.white;
                    var anim = visual.GetComponent<Animator>();
                    if (null == anim)
                    {
                        anim = visual.gameObject.AddComponent<Animator>();
                    }
                    anim.runtimeAnimatorController = aoc;
                    foreach (MonsterBrain brain in root.GetComponentsInChildren<MonsterBrain>(true))
                    {
                        SetProp(brain, "isDefaultLeft", true);
                    }
                    StyleWorldBar(root.GetComponentInChildren<SliderUI>(true));
                });
            }

            foreach (var pair in SKINS)
            {
                var dd = AssetDatabase.LoadAssetAtPath<DimensionData>(DIM_DATA + "/" + pair.Key + ".asset");
                var so = new SerializedObject(dd);
                SerializedProperty skins = so.FindProperty("monsterSkins");
                skins.arraySize = pair.Value.Length;
                for (int i = 0; i < pair.Value.Length; ++i)
                {
                    var (data, strip) = pair.Value[i];
                    SerializedProperty e = skins.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("monster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MonsterData>(MONSTER_DATA + "/" + data + ".asset");
                    e.FindPropertyRelative("animator").objectReferenceValue = MonsterOverride(ctrl, strip);
                    e.FindPropertyRelative("sprite").objectReferenceValue = Frames(ART + "/Monster/" + strip + "_Walk.png")[0];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.AppendLine("monsters ok");
        }

        #endregion

        #region Tile

        // tiles.py SHAPES 와 같은 순서. Forest 기존 타일과 같은 소켓 규칙 (Grass = 바깥 A, Sand = 안쪽 B)
        // 경계 타일 가중치가 높으면 맵이 흙 미로가 되므로 낮게 둔다
        private static readonly (string u, string d, string l, string r, float w)[] TILE_SOCKETS =
        {
            ("Grass", "Grass", "Grass", "Grass", 12f), ("Grass", "Grass", "Grass", "Grass", 0.5f),
            ("Grass", "Grass", "Grass", "Grass", 0.5f), ("Grass", "Grass", "Grass", "Grass", 0.5f),
            ("Sand", "Sand", "Sand", "Sand", 1.2f), ("Sand", "Sand", "Sand", "Sand", 0.2f), ("Sand", "Sand", "Sand", "Sand", 0.2f),
            ("Grass", "Left Bound", "Grass", "Up Bound", 0.08f),           // OuterTL
            ("Grass", "Sand", "Up Bound", "Up Bound", 0.08f),              // EdgeT
            ("Grass", "Right Bound", "Up Bound", "Grass", 0.08f),          // OuterTR
            ("Left Bound", "Left Bound", "Grass", "Sand", 0.08f),          // EdgeL
            ("Right Bound", "Right Bound", "Sand", "Grass", 0.08f),        // EdgeR
            ("Left Bound", "Grass", "Grass", "Down Bound", 0.08f),         // OuterBL
            ("Sand", "Grass", "Down Bound", "Down Bound", 0.08f),          // EdgeB
            ("Right Bound", "Grass", "Down Bound", "Grass", 0.08f),        // OuterBR
            ("Left Bound", "Sand", "Up Bound", "Sand", 0.08f),             // InnerTL
            ("Right Bound", "Sand", "Sand", "Up Bound", 0.08f),            // InnerTR
            ("Sand", "Left Bound", "Down Bound", "Sand", 0.08f),           // InnerBL
            ("Sand", "Right Bound", "Sand", "Down Bound", 0.08f),          // InnerBR
        };

        private static void BuildTiles()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/TileMap/ForestTile/ForstTile_1.prefab");
            var templateSr = template.GetComponent<SpriteRenderer>();
            var sockets = AssetDatabase.FindAssets("t:TileSocket", new[] { SOCKET_DIR })
                .Select(g => AssetDatabase.LoadAssetAtPath<TileSocket>(AssetDatabase.GUIDToAssetPath(g)))
                .ToDictionary(s => s.name);

            foreach (var (dim, asset, _) in PLAYERS)
            {
                string dir = TILE_DIR + "/" + dim;
                EnsureFolder(dir);
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(ART + "/TileMap/" + dim + "_Tile_Ground_Sheet.png").OfType<Sprite>().ToArray();
                var tileDatas = new List<TileData>();

                for (int i = 0; i < TILE_SHAPES.Length; ++i)
                {
                    string name = dim + "_Tile_Ground_" + TILE_SHAPES[i];
                    var go = new GameObject(name);
                    go.layer = template.layer;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprites.First(s => s.name == name);
                    sr.sortingLayerID = templateSr.sortingLayerID;
                    sr.sortingOrder = templateSr.sortingOrder;
                    sr.sharedMaterial = templateSr.sharedMaterial;
                    var td = go.AddComponent<TileData>();
                    var (u, d, l, r, w) = TILE_SOCKETS[i];
                    td.upSocket = sockets[u];
                    td.downSocket = sockets[d];
                    td.leftSocket = sockets[l];
                    td.rightSocket = sockets[r];
                    td.weight = w;
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + name + ".prefab");
                    Object.DestroyImmediate(go);
                    tileDatas.Add(saved.GetComponent<TileData>());
                }

                string loaderPath = "Assets/prefab/Managers/ChunkLoader_" + dim + ".prefab";
                if (null == AssetDatabase.LoadAssetAtPath<GameObject>(loaderPath))
                {
                    AssetDatabase.CopyAsset("Assets/prefab/Managers/chunkLoader 1.prefab", loaderPath);
                }
                EditPrefab(loaderPath, root =>
                {
                    var loader = root.GetComponent<WFCChunkLoader>();
                    loader.tileDatas = tileDatas.ToArray();
                    loader.tileScale = 1;
                });

                var dd = AssetDatabase.LoadAssetAtPath<DimensionData>(DIM_DATA + "/" + asset + ".asset");
                var so = new SerializedObject(dd);
                so.FindProperty("chunkLoaderPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WFCChunkLoader>(loaderPath);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.AppendLine("tiles ok");
        }

        #endregion

        #region Weapon / Item / Icon

        private static void SetVisual(string _prefab, string _child, string _sprite, Color? _color = null, string _layer = null, int? _order = null)
        {
            EditPrefab(_prefab, root =>
            {
                Transform t = string.IsNullOrEmpty(_child) ? root.transform : root.transform.Find(_child);
                if (null == t)
                {
                    log.AppendLine("missing child " + _prefab + " " + _child);
                    return;
                }
                var sr = t.GetComponent<SpriteRenderer>();
                sr.sprite = Single("Weapon/" + _sprite);
                if (_color.HasValue)
                {
                    sr.color = _color.Value;
                }
                if (null != _layer)
                {
                    sr.sortingLayerName = _layer;
                }
                if (_order.HasValue)
                {
                    sr.sortingOrder = _order.Value;
                }
            });
        }

        private static void BuildWeapons()
        {
            const string W = "Assets/prefab/weapon/";
            Color white = Color.white;
            SetVisual(W + "Boomerang/BoomerangProjectile.prefab", "visual", "D1_Weapon_Boomerang_Projectile", white);
            SetVisual(W + "SplitShot/SplitShotProjectile.prefab", "visual", "D1_Weapon_SplitShot_Projectile", white);
            SetVisual(W + "SplitShot/SplitShotFragment.prefab", "visual", "D1_Weapon_SplitShot_Fragment", white);
            SetVisual(W + "SplitShot/SplitShotBurst.prefab", "visual", "D1_Weapon_SplitShot_Burst", new Color(1f, 1f, 1f, 0.85f));
            SetVisual(W + "Sweep/SweepProjectile.prefab", "visual", "D1_Weapon_Sweep_Arc", white);
            SetVisual(W + "Sweep/SweepSlash.prefab", "visual", "D1_Weapon_Sweep_Slash", white);
            SetVisual(W + "PoisonZone/PoisonZoneProjectile.prefab", "visual", "D1_Weapon_PoisonZone_Area", new Color(1f, 1f, 1f, 0.75f));

            SetVisual(W + "Laser/LaserBeamProjectile.prefab", "visual", "D2_Weapon_Laser_Beam", white);
            SetVisual(W + "Drone/DroneProjectile.prefab", "visual", "D2_Weapon_Drone_Body", white);
            SetVisual(W + "Drone/DroneBullet.prefab", "visual", "D2_Weapon_Drone_Bullet", white);
            SetVisual(W + "Mine/MineProjectile.prefab", "visual", "D2_Weapon_Mine_Body", white);
            EditPrefab(W + "Mine/MineProjectile.prefab", root =>
            {
                foreach (MineProjectile mine in root.GetComponentsInChildren<MineProjectile>(true))
                {
                    SetProp(mine, "unarmedColor", new Color(1f, 1f, 1f, 0.55f));
                    SetProp(mine, "armedColor", Color.white);
                    SetProp(mine, "armedBlinkColor", new Color(1f, 0.45f, 0.4f, 1f));
                }
            });
            SetVisual(W + "Mine/MineExplosion.prefab", "visual", "Common_FX_Explosion", new Color(1f, 1f, 1f, 0.95f));

            SetVisual(W + "Explosive/ExplosiveProjectile.prefab", "visual", "D3_Weapon_Explosive_Projectile", white);
            SetVisual(W + "Explosive/ExplosiveExplosionEffect.prefab", "visual", "Common_FX_Explosion", new Color(1f, 1f, 1f, 0.95f));
            SetVisual(W + "Shockwave/ShockwaveProjectile.prefab", "visual", "D3_Weapon_Shockwave_Ring", new Color(0.65f, 0.9f, 1f, 0.9f));
            SetVisual(W + "Chain/ChainLightning.prefab", "visual", "D3_Weapon_Chain_Spark", white);

            // 체인 번개 라인: 텍스처 반복
            var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ART + "/Weapon/D3_Weapon_Chain_Line.png");
            string matPath = MAT + "/M_ChainLightning.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (null == mat)
            {
                mat = new Material(Shader.Find(UNLIT));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = lineTex;
            EditorUtility.SetDirty(mat);
            EditPrefab(W + "Chain/ChainLightning.prefab", root =>
            {
                foreach (LineRenderer lr in root.GetComponentsInChildren<LineRenderer>(true))
                {
                    lr.sharedMaterial = mat;
                    lr.textureMode = LineTextureMode.Tile;
                    lr.widthMultiplier = 0.3f;
                }
            });

            // 기본 탄 / 오라 / 궤도 / 체인 탄 (Default 정렬 레이어 금지 → attack)
            foreach (string p in new[] { "bullet/bullet", "bullet/AuraProjectile", "bullet/OrbitProjectile", "bullet/ChainProjectile" })
            {
                SetVisual(W + p + ".prefab", "visual", "Common_Weapon_Bullet", white, "attack");
            }

            // 몬스터 자폭 폭발 / 위험 지대
            EditPrefab(MONSTER_DIR + "/Bomber/BomberExplosion.prefab", root =>
            {
                foreach (string child in new[] { "visual", "core" })
                {
                    Transform t = root.transform.Find(child);
                    if (null != t)
                    {
                        var sr = t.GetComponent<SpriteRenderer>();
                        sr.sprite = Single("Weapon/Common_FX_Explosion");
                        sr.color = "core" == child ? new Color(1f, 1f, 0.85f, 0.9f) : new Color(1f, 0.85f, 0.8f, 0.95f);
                    }
                }
            });
            EditPrefab(MONSTER_DIR + "/HazardZone/HazardZone.prefab", root =>
            {
                var outer = root.GetComponent<SpriteRenderer>();
                outer.sprite = Single("Weapon/Common_Monster_HazardZone");
                outer.sortingLayerName = "BG";
                outer.sortingOrder = 8;
                Transform core = root.transform.Find("core");
                if (null != core)
                {
                    var csr = core.GetComponent<SpriteRenderer>();
                    csr.sprite = outer.sprite;
                    csr.sortingLayerName = "BG";
                    csr.sortingOrder = 9;
                }
            });
            log.AppendLine("weapons ok");
        }

        // 차원별 추가 무기 7종 (ExtraWeaponBuilder 가 만든 프리팹에 아트 적용, 크기는 판정 반경 기준)
        private static void SetScaleAt(Transform _t, float _x, float _y, float _sx, float _sy)
        {
            _t.localPosition = new Vector3(_x, _y, 0f);
            _t.localScale = new Vector3(_sx, _sy, 1f);
        }

        private static void StyleSprite(SpriteRenderer _sr, string _sprite, int _order)
        {
            _sr.sprite = Single("Weapon/" + _sprite);
            _sr.color = Color.white;
            _sr.sortingLayerName = "attack";
            _sr.sortingOrder = _order;
        }

        private static void BuildExtraWeapons()
        {
            const string W = "Assets/prefab/weapon/";
            // 스프라이트 32px = 1 유닛. 루트 크기(ScaleOverLifetime) 가 반경 / 0.5 로 곱해지므로 visual 은 크기 1
            EditPrefab(W + "Vine/VineProjectile.prefab", root =>
            {
                Transform v = root.transform.Find("visual");
                StyleSprite(v.GetComponent<SpriteRenderer>(), "D1_Weapon_Vine_Segment", 0);
                SetScaleAt(v, 0f, 0f, 1f, 1f);
            });
            EditPrefab(W + "Vine/VineThorn.prefab", root =>
            {
                Transform v = root.transform.Find("visual");
                StyleSprite(v.GetComponent<SpriteRenderer>(), "D1_Weapon_Vine_Thorn", 1);
                SetScaleAt(v, 0f, 0f, 0.5f, 0.5f);
            });
            EditPrefab(W + "Root/RootProjectile.prefab", root =>
            {
                Transform v = root.transform.Find("visual");
                StyleSprite(v.GetComponent<SpriteRenderer>(), "D1_Weapon_Root_Area", 0);
                v.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.85f);
                SetScaleAt(v, 0f, 0f, 1f, 1f);
            });
            EditPrefab(W + "Spirit/SpiritProjectile.prefab", root =>
            {
                StyleSprite(root.GetComponent<SpriteRenderer>(), "D1_Weapon_Spirit_Body", 1);
                root.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            });
            EditPrefab(W + "Saw/SawProjectile.prefab", root =>
            {
                Transform v = root.transform.Find("visual");
                StyleSprite(v.GetComponent<SpriteRenderer>(), "D2_Weapon_Saw_Blade", 1);
                SetScaleAt(v, 0f, 0f, 1f, 1f);
            });
            EditPrefab(W + "Flame/FlameProjectile.prefab", root =>
            {
                Transform visual = root.transform.Find("visual");
                Transform first = visual.Find("flame");
                var tiles = new (string n, float x, float w, float h, int order)[]
                {
                    ("flame", 2.3f, 1.4f, 3.0f, 0), ("flame2", 1.4f, 1.6f, 1.6f, 1), ("flame3", 0.55f, 0.9f, 0.9f, 2),
                };
                foreach (var t in tiles)
                {
                    Transform tt = visual.Find(t.n);
                    if (null == tt)
                    {
                        tt = new GameObject(t.n).transform;
                        tt.SetParent(visual, false);
                        tt.gameObject.AddComponent<SpriteRenderer>();
                    }
                    StyleSprite(tt.GetComponent<SpriteRenderer>(), "D2_Weapon_Flame_Fire", t.order);
                    SetScaleAt(tt, t.x, 0f, t.w, t.h);
                }
            });
            EditPrefab(W + "Tornado/TornadoProjectile.prefab", root =>
            {
                Transform v = root.transform.Find("visual");
                StyleSprite(v.GetComponent<SpriteRenderer>(), "D3_Weapon_Tornado_Body", 1);
                SetScaleAt(v, 0f, 0f, 1.6f, 1.6f);
            });

            // 낙뢰: 표식(지름 1 = markerUnitRadius 0.5) + 번개 라인은 체인 번개와 같은 텍스처 반복
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT + "/M_ChainLightning.mat");
            EditPrefab(W + "Lightning/LightningProjectile.prefab", root =>
            {
                Transform m = root.transform.Find("Marker");
                StyleSprite(m.GetComponent<SpriteRenderer>(), "D3_Weapon_Lightning_Marker", 0);
                m.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.85f);
                SetScaleAt(m, 0f, 0f, 1f, 1f);
                foreach (var (n, width) in new[] { ("Bolt", 0.35f), ("Link", 0.22f) })
                {
                    var lr = root.transform.Find(n).GetComponent<LineRenderer>();
                    lr.sharedMaterial = mat;
                    lr.textureMode = LineTextureMode.Tile;
                    lr.startWidth = width;
                    lr.endWidth = width;
                    lr.sortingLayerName = "attack";
                    lr.sortingOrder = 2;
                }
            });
            log.AppendLine("extra weapons ok");
        }

        private static void BuildItems()
        {
            EditPrefab("Assets/prefab/Item/expGem.prefab", root =>
            {
                Transform visual = root.transform.Find("visual");
                // 루트가 커져 있어도 월드 크기가 플레이어보다 작게 (16px 그림 기준 0.6배)
                float parent = null != visual.parent ? visual.parent.lossyScale.x : 1f;
                visual.localScale = Vector3.one * (0.6f / Mathf.Max(0.01f, parent));
                // 초록 슬라임과 헷갈리지 않게 노란 보석
                visual.GetComponent<SpriteRenderer>().sprite = Single("Item/Common_Item_ExpGem_Yellow");
            });
            log.AppendLine("items ok");
        }

        // RewardData / 업그레이드 테이블 이름 → 아이콘
        private static readonly (string prefix, string icon)[] ICONS =
        {
            ("Boomerang", "Boomerang"), ("PoisonZone", "PoisonZone"), ("Sweep", "Sweep"), ("SplitShot", "SplitShot"),
            ("Laser", "Laser"), ("Drone", "Drone"), ("Mine", "Mine"), ("Chain", "Chain"), ("Shockwave", "Shockwave"),
            ("Explosive", "Explosive"),
            ("Vine", "Vine"), ("Spirit", "Spirit"), ("Root", "Root"), ("Saw", "Saw"),
            ("Flame", "Flame"), ("Tornado", "Tornado"), ("Lightning", "Lightning"),
            ("ASUpgrade", "AttackSpeedUp"), ("DamageUpgrade", "DamageUp"), ("HPUpgrade", "HPUp"),
            ("RangeUpgrade", "RangeUp"), ("SpeedUpgrade", "SpeedUp"),
            ("AttackNuff", "AttackSpeedDown"), ("DamageNuff", "DamageDown"), ("HPNuff", "HPDown"),
            ("MovespeedNuff", "SpeedDown"), ("RangeNuff", "RangeDown"),
        };

        private static void BuildIcons()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/prefab/Upgrade" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                var match = ICONS.FirstOrDefault(m => file.StartsWith(m.prefix));
                if (null == match.prefix)
                {
                    continue;
                }
                Sprite icon = Single("Icon/Common_Icon_" + match.icon);
                foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (null == obj)
                    {
                        continue;
                    }
                    var so = new SerializedObject(obj);
                    SerializedProperty it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (SerializedPropertyType.ObjectReference == it.propertyType && "image" == it.name)
                        {
                            it.objectReferenceValue = icon;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        ++count;
                    }
                }
            }
            log.AppendLine("icons " + count);
        }

        #endregion

        #region UI

        private static TMP_FontAsset BuildFont()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET);
            if (null == fa)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FONT_TTF);
                fa = TMP_FontAsset.CreateFontAsset(font, 24, 2, GlyphRenderMode.RASTER_HINTED, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = "Galmuri11 Pixel";
                AssetDatabase.CreateAsset(fa, FONT_ASSET);
                fa.atlasTextures[0].name = "Galmuri11 Pixel Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = "Galmuri11 Pixel Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            fa.atlasTextures[0].filterMode = FilterMode.Point;

            // 갈무리에 없는 글자는 기존 Noto 로
            var noto = AssetDatabase.FindAssets("NotoSansKR-Regular SDF t:TMP_FontAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (null != noto)
            {
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { noto };
            }
            EditorUtility.SetDirty(fa);
            log.AppendLine("font ok");
            return fa;
        }

        // 픽셀 폰트는 12 배수 크기에서 선명하다
        private static void ApplyFont(GameObject _root, TMP_FontAsset _font)
        {
            foreach (TMP_Text t in _root.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = _font;
                t.fontSharedMaterial = _font.material;
                t.fontSize = Mathf.Max(12f, Mathf.Round(t.fontSize / 12f) * 12f);
                if (t.enableAutoSizing)
                {
                    t.fontSizeMin = Mathf.Max(12f, Mathf.Round(t.fontSizeMin / 12f) * 12f);
                    t.fontSizeMax = Mathf.Max(t.fontSizeMin, Mathf.Round(t.fontSizeMax / 12f) * 12f);
                }
                EditorUtility.SetDirty(t);
            }
        }

        private static void Sliced(Image _image, Sprite _sprite, Color? _color = null)
        {
            if (null == _image)
            {
                return;
            }
            _image.sprite = _sprite;
            _image.type = Image.Type.Sliced;
            _image.pixelsPerUnitMultiplier = UI_PIXEL_MULT;
            if (_color.HasValue)
            {
                _image.color = _color.Value;
            }
            EditorUtility.SetDirty(_image);
        }

        // 머리 위 작은 체력 바 (테두리 그림 + 같은 크기의 채움을 fillAmount 로 자른다)
        private static void StyleWorldBar(SliderUI _slider)
        {
            if (null == _slider)
            {
                return;
            }
            _slider.backgroundSprite = Single("UI/Common_UI_Bar_Monster_Frame");
            _slider.fillSprite = Single("UI/Common_UI_Bar_Monster_Fill");
            _slider.backgroundColor = Color.white;
            _slider.fillColor = Color.white;
            if (null != _slider.background)
            {
                _slider.background.sprite = _slider.backgroundSprite;
                _slider.background.color = Color.white;
                _slider.background.type = Image.Type.Simple;
            }
            if (null != _slider.fill)
            {
                _slider.fill.sprite = _slider.fillSprite;
                _slider.fill.color = Color.white;
                _slider.fill.type = Image.Type.Filled;
                _slider.fill.fillMethod = Image.FillMethod.Horizontal;
            }
            EditorUtility.SetDirty(_slider);
        }

        // 화면 게이지: 어두운 바탕 + 늘어나는 채움 + 위에 겹치는 테두리
        private static void StyleScreenBar(SliderUI _slider, string _fill)
        {
            if (null == _slider)
            {
                return;
            }
            _slider.backgroundSprite = null;
            _slider.backgroundColor = new Color32(36, 30, 44, 255);
            _slider.fillSprite = Single("UI/" + _fill);
            _slider.fillColor = Color.white;
            if (null != _slider.background)
            {
                _slider.background.sprite = null;
                _slider.background.color = _slider.backgroundColor;
            }
            if (null != _slider.fill)
            {
                Sliced(_slider.fill, _slider.fillSprite, Color.white);
            }

            Transform frame = _slider.transform.Find("Frame");
            if (null == frame)
            {
                var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                go.layer = _slider.gameObject.layer;
                frame = go.transform;
                frame.SetParent(_slider.transform, false);
            }
            frame.SetAsLastSibling();
            var rt = (RectTransform)frame;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = frame.GetComponent<Image>();
            image.raycastTarget = false;
            Sliced(image, Single("UI/Common_UI_Bar_Frame"), Color.white);
            EditorUtility.SetDirty(_slider);
        }

        private static void StyleButton(Button _button)
        {
            if (null == _button)
            {
                return;
            }
            Sliced(_button.image, Single("UI/Common_UI_Button_Normal"), Color.white);
            _button.transition = Selectable.Transition.SpriteSwap;
            _button.spriteState = new SpriteState
            {
                highlightedSprite = Single("UI/Common_UI_Button_Hover"),
                selectedSprite = Single("UI/Common_UI_Button_Hover"),
                pressedSprite = Single("UI/Common_UI_Button_Pressed"),
            };
            EditorUtility.SetDirty(_button);
        }

        private static void StyleSelectPanels(GameObject _root)
        {
            foreach (Transform t in _root.GetComponentsInChildren<Transform>(true))
            {
                if ("BG" == t.name && null != t.GetComponent<Image>())
                {
                    Sliced(t.GetComponent<Image>(), Single("UI/Common_UI_PanelDark"), Color.white);
                }
                else if ("Selected BG" == t.name && null != t.GetComponent<Image>())
                {
                    Sliced(t.GetComponent<Image>(), Single("UI/Common_UI_Panel"), Color.white);
                }
            }
        }

        private static void BuildPrefabUI(TMP_FontAsset _font)
        {
            EditPrefab("Assets/prefab/UI/EXPUI.prefab", root =>
            {
                StyleScreenBar(root.GetComponentInChildren<SliderUI>(true), "Common_UI_Bar_EXP_Fill");
                ApplyFont(root, _font);
            });
            EditPrefab("Assets/prefab/UI/Select Panel.prefab", root =>
            {
                StyleSelectPanels(root);
                ApplyFont(root, _font);
            });
            EditPrefab("Assets/prefab/UI/LevelUpUI.prefab", root =>
            {
                StyleSelectPanels(root);
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                {
                    StyleButton(b);
                }
                ApplyFont(root, _font);
            });

            // 다른 프리팹의 TMP (데미지 숫자 등)
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (null != go.GetComponentInChildren<TMP_Text>(true) && false == path.Contains("/UI/"))
                {
                    EditPrefab(path, root => ApplyFont(root, _font));
                }
            }
            log.AppendLine("prefab ui ok");
        }

        private static void BuildScenes(TMP_FontAsset _font)
        {
            string active = EditorSceneManager.GetActiveScene().path;

            EditScene("Assets/Scenes/TitleScene.unity", roots =>
            {
                Transform canvas = FindIn(roots, "TitleCanvas");
                // TitleSceneBuilder 가 만든 기존 배경(RawImage, fbg.png)은 끈다
                Transform oldBg = canvas.Find("Background");
                if (null != oldBg && null != oldBg.GetComponent<RawImage>())
                {
                    oldBg.gameObject.SetActive(false);
                }
                Image bg = GetOrCreateImage(canvas, "TitleBackground");
                bg.transform.SetAsFirstSibling();
                bg.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                bg.rectTransform.anchoredPosition = Vector2.zero;
                bg.sprite = Single("Title/Common_Title_Background");
                bg.type = Image.Type.Simple;
                bg.color = Color.white;
                bg.raycastTarget = false;
                // 16:9 가 아닌 화면에서도 비율을 지키며 꽉 차도록 (넘치는 쪽은 잘림)
                var fitter = bg.GetComponent<AspectRatioFitter>();
                if (null == fitter)
                {
                    fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
                }
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = 16f / 9f;

                Transform logoText = canvas.Find("Logo");
                Image logo = GetOrCreateImage(canvas, "LogoImage");
                if (null != logoText)
                {
                    var src = (RectTransform)logoText;
                    var dst = logo.rectTransform;
                    dst.anchorMin = src.anchorMin;
                    dst.anchorMax = src.anchorMax;
                    dst.pivot = src.pivot;
                    dst.anchoredPosition = src.anchoredPosition;
                    logoText.gameObject.SetActive(false);
                }
                logo.sprite = Single("Title/Common_Title_Logo");
                logo.color = Color.white;
                logo.raycastTarget = false;
                logo.rectTransform.sizeDelta = new Vector2(logo.sprite.rect.width * 3f, logo.sprite.rect.height * 3f);

                foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
                {
                    StyleButton(b);
                }
                foreach (Transform r in roots)
                {
                    ApplyFont(r.gameObject, _font);
                }
            });

            EditScene("Assets/Scenes/DimensionSelectScene.unity", roots =>
            {
                foreach (Image image in roots.SelectMany(r => r.GetComponentsInChildren<Image>(true)))
                {
                    if (image.name.EndsWith("Card"))
                    {
                        Sliced(image, Single("UI/Common_UI_Panel2"));
                    }
                    else if (image.name.EndsWith("ConfirmedMark"))
                    {
                        Sliced(image, Single("UI/Common_UI_PanelFocus"));
                    }
                    else if ("Preview" == image.name)
                    {
                        image.preserveAspect = true;
                        EditorUtility.SetDirty(image);
                    }
                }
                foreach (Transform r in roots)
                {
                    ApplyFont(r.gameObject, _font);
                }
            });

            EditScene("Assets/Scenes/GameScene.unity", roots =>
            {
                foreach (Image image in roots.SelectMany(r => r.GetComponentsInChildren<Image>(true)))
                {
                    if ("LeftCard" == image.name || "RightCard" == image.name)
                    {
                        Sliced(image, Single("UI/Common_UI_PanelDark"), Color.white);
                    }
                }
                foreach (SliderUI slider in roots.SelectMany(r => r.GetComponentsInChildren<SliderUI>(true)))
                {
                    Canvas canvas = slider.GetComponentInParent<Canvas>(true);
                    if (null != canvas && RenderMode.WorldSpace == canvas.renderMode)
                    {
                        StyleWorldBar(slider);
                    }
                    else
                    {
                        StyleScreenBar(slider, "Common_UI_Bar_EXP_Fill");
                    }
                }
                foreach (Button b in roots.SelectMany(r => r.GetComponentsInChildren<Button>(true)))
                {
                    StyleButton(b);
                }
                foreach (Transform r in roots)
                {
                    StyleSelectPanels(r.gameObject);
                    ApplyFont(r.gameObject, _font);
                }
            });

            if (false == string.IsNullOrEmpty(active))
            {
                EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
            }
            log.AppendLine("scenes ok");
        }

        #endregion

        #region Util

        private static void EditPrefab(string _path, System.Action<GameObject> _edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(_path);
            try
            {
                _edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, _path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EditScene(string _path, System.Action<Transform[]> _edit)
        {
            var scene = EditorSceneManager.OpenScene(_path, OpenSceneMode.Single);
            _edit(scene.GetRootGameObjects().Select(g => g.transform).ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Transform FindIn(Transform[] _roots, string _name)
        {
            return _roots.FirstOrDefault(r => r.name == _name);
        }

        private static Image GetOrCreateImage(Transform _parent, string _name)
        {
            Transform t = _parent.Find(_name);
            if (null == t)
            {
                var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
                go.layer = _parent.gameObject.layer;
                t = go.transform;
                t.SetParent(_parent, false);
            }
            return t.GetComponent<Image>();
        }

        private static void SetProp(Object _target, string _name, object _value)
        {
            var so = new SerializedObject(_target);
            SerializedProperty p = so.FindProperty(_name);
            if (null == p)
            {
                log.AppendLine("no prop " + _target.GetType().Name + "." + _name);
                return;
            }
            switch (_value)
            {
                case bool b: p.boolValue = b; break;
                case Color c: p.colorValue = c; break;
                case Object o: p.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string _path)
        {
            string[] parts = _path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                string next = cur + "/" + parts[i];
                if (false == AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }

        #endregion
    }
}
