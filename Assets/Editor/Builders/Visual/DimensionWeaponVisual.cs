using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// 차원 무기 6종 + 합성 카드 시각 적용 (멱등). ArtResourceBuilder.Build 끝에서도 호출된다
    /// - 새 아트 없이 기존 Weapon/Icon 텍스처를 재사용, 테마색(숲 녹색 + 공장 주황) 테두리/틴트
    /// - 외형 프리팹: Assets/prefab/weapon/Dimension/Parts/
    /// </summary>
    public static class DimensionWeaponVisual
    {
        private const string ART = "Assets/Resources/Textuer/Art";
        private const string DIM = "Assets/prefab/weapon/Dimension";
        private const string PARTS = DIM + "/Parts";
        private const string MAT = "Assets/prefab/Art/Material";
        private const string UNLIT = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float UI_PIXEL_MULT = 100f / 32f / 2f;

        private static readonly Color Forest = new Color(0.45f, 1f, 0.5f, 1f);
        private static readonly Color Factory = new Color(1f, 0.62f, 0.2f, 1f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color Purple = new Color(0.62f, 0.4f, 1f, 1f);

        private static readonly StringBuilder log = new StringBuilder();

        public static string Build()
        {
            log.Clear();
            EnsureFolder(PARTS);
            EnsureFolder(MAT);

            BuildSquadron();
            BuildThornSaw();
            BuildPhantomWhirl();
            BuildPrismLaser();
            BuildPlagueMine();
            BuildBlazeBoomerang();
            BuildFusionCard();
            BuildIcons();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.AppendLine("DimensionWeaponVisual ok");
            return log.ToString();
        }

        #region Weapons

        private static void BuildSquadron()
        {
            GameObject spirit = MakeVisual("DimSpirit", "D1_Weapon_Spirit_Body", Color.white, Forest, 1.0f, 1.45f, 10, 0.95f);
            GameObject drone = MakeVisual("DimDrone", "D2_Weapon_Drone_Body", Color.white, Factory, 0.5f, 1.4f, 10, 0.95f);
            GameObject bullet = MakeVisual("DimDroneBullet", "D2_Weapon_Drone_Bullet", new Color(1f, 0.95f, 0.8f), Factory, 0.28f, 1.6f, 12, 0.8f);
            EditPrefab(DIM + "/SpiritSquadron.prefab", root =>
            {
                var w = root.GetComponent<SpiritSquadron>();
                SetObj(w, "spiritPrefab", spirit);
                SetObj(w, "dronePrefab", drone);
                SetObj(w, "bulletPrefab", bullet);
                SetFloat(w, "spiritScale", 1f);
                SetFloat(w, "droneScale", 1f);
                SetFloat(w, "bulletScale", 1f);
                SetStr(w, "sortingLayerName", "attack");
                SetLayer(root);
            });
        }

        private static void BuildThornSaw()
        {
            GameObject saw = MakeVisual("DimSaw", "D2_Weapon_Saw_Blade", new Color(1f, 0.92f, 0.8f), Factory, 1.2f, 1.2f, 12, 0.9f);
            GameObject vine = MakeVisual("DimVine", "D1_Weapon_Vine_Segment", new Color(0.8f, 1f, 0.75f), Forest, 1.1f, 1.25f, 9, 0.7f);
            EditPrefab(DIM + "/ThornSaw.prefab", root =>
            {
                var w = root.GetComponent<ThornSaw>();
                SetObj(w, "sawPrefab", saw);
                SetObj(w, "vinePrefab", vine);
                SetFloat(w, "sawScale", 1f);
                SetFloat(w, "vineScale", 1f);
                SetStr(w, "sortingLayerName", "attack");
                SetLayer(root);
            });
        }

        private static void BuildPhantomWhirl()
        {
            EditPrefab(DIM + "/PhantomWhirl.prefab", root =>
            {
                var w = root.GetComponent<PhantomWhirl>();
                SetObj(w, "bladeSprite", Sprite("Weapon/D2_Weapon_SpinBlade_Blade"));
                SetColor(w, "bladeColor", new Color(0.85f, 1f, 0.75f, 1f));
                SetColor(w, "phantomColor", new Color(0.7f, 0.45f, 1f, 0.45f));
                SetFloat(w, "visualWidth", 1f);
                SetStr(w, "sortingLayerName", "attack");
                SetInt(w, "sortingOrder", 10);
                SetLayer(root);
            });
        }

        private static void BuildPrismLaser()
        {
            string matPath = MAT + "/M_DimPrismBeam.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (null == mat)
            {
                mat = new Material(Shader.Find(UNLIT));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ART + "/Weapon/D2_Weapon_Laser_Beam.png");
            EditorUtility.SetDirty(mat);
            EditPrefab(DIM + "/PrismLaser.prefab", root =>
            {
                var w = root.GetComponent<PrismLaser>();
                SetObj(w, "beamMaterial", mat);
                SetColor(w, "primaryColor", new Color(1f, 0.78f, 0.4f, 1f));
                SetColor(w, "branchColor", new Color(0.5f, 1f, 0.6f, 1f));
                SetFloat(w, "primaryVisualWidth", 0.4f);
                SetFloat(w, "branchVisualWidth", 0.22f);
                SetStr(w, "sortingLayerName", "attack");
                SetLayer(root);
            });
        }

        private static void BuildPlagueMine()
        {
            // 지뢰 (공장 주황 테두리)
            GameObject mine = MakeVisual("PlagueMineVisual", "D2_Weapon_Mine_Body", new Color(0.85f, 1f, 0.7f), Factory, 0.55f, 1.35f, 5, 0.9f);
            string minePath = PARTS + "/PlagueMineBomb.prefab";
            GameObject mineRoot = EnsurePrefab(minePath, "PlagueMineBomb", r => r.AddComponent<PlagueMineBomb>());
            EditPrefab(minePath, root =>
            {
                Transform old = root.transform.Find("visual");
                if (null != old) { Object.DestroyImmediate(old.gameObject); }
                var v = (GameObject)PrefabUtility.InstantiatePrefab(mine, root.transform);
                v.name = "visual";
                var b = root.GetComponent<PlagueMineBomb>();
                SetStr(b, "sortingLayerName", "attack");
                SetLayer(root);
            });

            // 독 장판 (숲 녹색 + 주황 테두리 고리, scale 1 = 반지름 0.5)
            string zonePath = PARTS + "/PlagueZone.prefab";
            EnsurePrefab(zonePath, "PlagueZone", r => r.AddComponent<PlagueZone>());
            EditPrefab(zonePath, root =>
            {
                ClearChildren(root);
                var ring = MakeSprite(root.transform, "ring", "D1_Weapon_PoisonZone_Area", new Color(1f, 0.6f, 0.2f, 0.4f), 1.08f, 2);
                var area = MakeSprite(root.transform, "visual", "D1_Weapon_PoisonZone_Area", new Color(0.55f, 1f, 0.4f, 0.7f), 1f, 3);
                var z = root.GetComponent<PlagueZone>();
                SetFloat(z, "unitRadius", 0.5f);
                SetStr(z, "sortingLayerName", "attack");
                SetLayer(root);
            });

            // 폭발 (원본 MineExplosion 복제 + 녹색 틴트)
            string expPath = PARTS + "/PlagueMineExplosion.prefab";
            if (null == AssetDatabase.LoadAssetAtPath<GameObject>(expPath))
            {
                AssetDatabase.CopyAsset("Assets/prefab/weapon/Mine/MineExplosion.prefab", expPath);
            }
            EditPrefab(expPath, root =>
            {
                foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.color = new Color(0.65f, 1f, 0.45f, 0.95f);
                    sr.sortingLayerName = "attack";
                }
                SetLayer(root);
            });

            EditPrefab(DIM + "/PlagueMine.prefab", root =>
            {
                var w = root.GetComponent<PlagueMine>();
                SetObj(w, "minePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(minePath).GetComponent<PlagueMineBomb>());
                SetObj(w, "zonePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(zonePath).GetComponent<PlagueZone>());
                SetObj(w, "explosionEffect", AssetDatabase.LoadAssetAtPath<GameObject>(expPath).GetComponent<PooledEffect>());
                SetLayer(root);
            });
        }

        private const int BLAZE_JET_COUNT = 4;

        /// <summary>
        /// 화염 분출구 하나. _parent(회전하는 visual) 기준 _angle 방향 끝에 두고 그 방향으로 원뿔 분출
        /// </summary>
        private static ParticleSystem CreateBlazeJet(Transform _parent, int _index, float _angle, Material _mat)
        {
            var go = new GameObject("jet_" + _index);
            go.transform.SetParent(_parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
            go.transform.localPosition = go.transform.localRotation * new Vector3(0f, 0.35f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f, 0.95f), new Color(1f, 0.85f, 0.3f, 0.95f));
            main.maxParticles = 60;

            var em = ps.emission;
            em.rateOverTime = 45f;

            // 원뿔 기본 축(+Z)을 로컬 +Y(분출구 바깥쪽)로 돌린다
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // 뿜어진 뒤 감속하며 퍼진다
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 3.2f, 1f, 0.6f));
            limit.dampen = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.5f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = _mat;
            pr.sortingLayerName = "attack";
            pr.sortingOrder = 8;
            return ps;
        }

        private static void BuildBlazeBoomerang()
        {
            string matPath = MAT + "/M_DimFlame.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (null == mat)
            {
                mat = new Material(Shader.Find(UNLIT));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ART + "/Weapon/D2_Weapon_Flame_Fire.png");
            EditorUtility.SetDirty(mat);

            string path = PARTS + "/BlazeBoomerangProjectile.prefab";
            EnsurePrefab(path, "BlazeBoomerangProjectile", r => r.AddComponent<BlazeBoomerangProjectile>());
            EditPrefab(path, root =>
            {
                ClearChildren(root);
                // 회전 대상: 부메랑 (숲 녹색 테두리 + 주황 틴트)
                var spin = new GameObject("visual");
                spin.transform.SetParent(root.transform, false);
                MakeSprite(spin.transform, "glow", "D1_Weapon_Boomerang_Projectile", new Color(0.45f, 1f, 0.5f, 0.75f), 1.95f, 8);
                MakeSprite(spin.transform, "body", "D1_Weapon_Boomerang_Projectile", new Color(1f, 0.78f, 0.45f, 1f), 1.5f, 9);

                // 화염 파티클 (월드 시뮬레이션: 궤적을 따라 불꽃이 남는다)
                var fire = new GameObject("flame");
                fire.transform.SetParent(root.transform, false);
                var ps = fire.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 0.45f;
                main.startSpeed = 0.4f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.85f, 0.3f, 0.9f));
                main.maxParticles = 80;
                var em = ps.emission;
                em.rateOverTime = 40f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.25f;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = grad;
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
                var pr = fire.GetComponent<ParticleSystemRenderer>();
                pr.sharedMaterial = mat;
                pr.sortingLayerName = "attack";
                pr.sortingOrder = 7;

                // 사방 화염 분출: 회전하는 visual 의 자식으로 분출구 BLAZE_JET_COUNT 개(등간격)를 두고 바깥쪽으로 뿜는다
                // 월드 시뮬레이션이라 분출 순간의 방향으로 날아가 회전에 따라 나선형으로 퍼진다
                // 도달 거리 ≈ 분출구 위치 0.35 + 속도 × 수명(≈ 2.8 × 0.38 ≈ 1.06) → 화염 데미지 반지름(1.2) 근처
                var particleList = new List<ParticleSystem> { ps };
                for (int j = 0; j < BLAZE_JET_COUNT; ++j)
                {
                    particleList.Add(CreateBlazeJet(spin.transform, j, 360f / BLAZE_JET_COUNT * j, mat));
                }

                var p = root.GetComponent<BlazeBoomerangProjectile>();
                SetObj(p, "spinTransform", spin.transform);
                SetStr(p, "sortingLayerName", "attack");
                var so = new SerializedObject(p);
                SerializedProperty arr = so.FindProperty("particles");
                arr.arraySize = particleList.Count;
                for (int j = 0; j < particleList.Count; ++j)
                {
                    arr.GetArrayElementAtIndex(j).objectReferenceValue = particleList[j];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                SetLayer(root);
            });

            EditPrefab(DIM + "/BlazeBoomerang.prefab", root =>
            {
                var w = root.GetComponent<BlazeBoomerang>();
                SetObj(w, "projectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<BlazeBoomerangProjectile>());
                SetLayer(root);
            });
        }

        #endregion

        #region Fusion card

        private static void BuildFusionCard()
        {
            EditPrefab("Assets/prefab/UI/LevelUpUI.prefab", root =>
            {
                var card = root.GetComponentInChildren<DimensionFusionCard>(true);
                if (null == card)
                {
                    log.AppendLine("fusion card missing");
                    return;
                }
                Transform t = card.transform;

                // 테두리 = 금색 패널, 안쪽 = 보라 어두운 패널
                var frame = t.GetComponent<Image>();
                SlicedImage(frame, "UI/Common_UI_Panel", Gold);
                Transform inner = t.Find("InnerPanel");
                if (null == inner)
                {
                    var go = new GameObject("InnerPanel", typeof(RectTransform), typeof(Image));
                    inner = go.transform;
                    inner.SetParent(t, false);
                    inner.SetAsFirstSibling();
                }
                var irt = (RectTransform)inner;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(10f, 10f);
                irt.offsetMax = new Vector2(-10f, -10f);
                var iimg = inner.GetComponent<Image>();
                SlicedImage(iimg, "UI/Common_UI_PanelDark", new Color(0.6f, 0.45f, 0.95f, 1f));
                iimg.raycastTarget = false;

                // 선택 배경 (금색 광채)
                Transform sb = t.Find("SelectBackground");
                if (null != sb)
                {
                    var rt = (RectTransform)sb;
                    rt.offsetMin = new Vector2(-8f, -8f);
                    rt.offsetMax = new Vector2(8f, 8f);
                    var img = sb.GetComponent<Image>();
                    SlicedImage(img, "UI/Common_UI_Panel", new Color(1f, 0.85f, 0.3f, 0.55f));
                    img.raycastTarget = false;
                    sb.SetSiblingIndex(0);
                }

                // 커서 막대 (왼쪽/오른쪽 진영 색)
                SetBar(t.Find("LeftCursor"), new Color(0.3f, 0.7f, 1f, 1f));
                SetBar(t.Find("RightCursor"), new Color(1f, 0.4f, 0.4f, 1f));

                // "+" 와 제목 색
                var plus = t.Find("Plus")?.GetComponent<TMP_Text>();
                if (null != plus) { plus.color = Gold; EditorUtility.SetDirty(plus); }
                var title = t.Find("Title")?.GetComponent<TMP_Text>();
                if (null != title) { title.color = Gold; EditorUtility.SetDirty(title); }

                // 재료 아이콘 배경 (테마색 틴트 박스)
                MaterialBox(t, "MaterialIconA", "MatBoxA", Forest);
                MaterialBox(t, "MaterialIconB", "MatBoxB", Factory);
            });
            log.AppendLine("fusion card ok");
        }

        private static void MaterialBox(Transform _card, string _icon, string _name, Color _color)
        {
            Transform icon = _card.Find(_icon);
            if (null == icon) { return; }
            Transform box = _card.Find(_name);
            if (null == box)
            {
                var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
                box = go.transform;
                box.SetParent(_card, false);
            }
            var rt = (RectTransform)box;
            var src = (RectTransform)icon;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition;
            rt.sizeDelta = src.sizeDelta + new Vector2(16f, 16f);
            var img = box.GetComponent<Image>();
            SlicedImage(img, "UI/Common_UI_Panel", new Color(_color.r, _color.g, _color.b, 0.9f));
            img.raycastTarget = false;
            // 박스가 아이콘 바로 뒤(아래)에 오도록
            box.SetSiblingIndex(Mathf.Min(box.GetSiblingIndex(), icon.GetSiblingIndex()));
            icon.SetSiblingIndex(box.GetSiblingIndex() + 1);
        }

        private static void SetBar(Transform _t, Color _c)
        {
            if (null == _t) { return; }
            var img = _t.GetComponent<Image>();
            img.color = _c;
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
        }

        private static void SlicedImage(Image _img, string _sprite, Color _color)
        {
            if (null == _img) { return; }
            _img.sprite = Sprite(_sprite);
            _img.type = Image.Type.Sliced;
            _img.pixelsPerUnitMultiplier = UI_PIXEL_MULT;
            _img.color = _color;
            EditorUtility.SetDirty(_img);
        }

        #endregion

        #region Icons

        // 두 재료 아이콘을 대각선으로 합친 아이콘 (A 좌상단 / B 우하단, 약 62%)
        private static void BuildIcons()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:DimensionWeaponData", new[] { "Assets/prefab/Upgrade/Dimension" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<DimensionWeaponData>(path);
                var so = new SerializedObject(data);
                var a = so.FindProperty("materialA").objectReferenceValue as Object;
                var b = so.FindProperty("materialB").objectReferenceValue as Object;
                if (null == a || null == b) { continue; }
                string pa = ART + "/Icon/Common_Icon_" + a.name + ".png";
                string pb = ART + "/Icon/Common_Icon_" + b.name + ".png";
                if (false == File.Exists(pa) || false == File.Exists(pb))
                {
                    log.AppendLine("icon source missing " + a.name + "/" + b.name);
                    continue;
                }
                string outPath = ART + "/Icon/Common_Icon_Dim_" + data.name.Replace("Data", "") + ".png";
                if (false == File.Exists(outPath))
                {
                    var ta = Load(pa);
                    var tb = Load(pb);
                    int w = ta.width;
                    int h = ta.height;
                    var res = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    var clear = new Color[w * h];
                    res.SetPixels(clear);
                    int s = Mathf.RoundToInt(w * 0.62f);
                    Blit(res, tb, w - s, 0, s);
                    Blit(res, ta, 0, h - s, s);
                    File.WriteAllBytes(outPath, res.EncodeToPNG());
                    Object.DestroyImmediate(res);
                    AssetDatabase.ImportAsset(outPath);
                    var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spritePixelsPerUnit = 32f;
                    ti.filterMode = FilterMode.Point;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
                so.FindProperty("icon").objectReferenceValue = spr;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
            log.AppendLine("icons ok");
        }

        private static Texture2D Load(string _path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(t, File.ReadAllBytes(_path));
            return t;
        }

        // 최근접 샘플링 축소 합성 (알파 오버)
        private static void Blit(Texture2D _dst, Texture2D _src, int _x, int _y, int _size)
        {
            for (int y = 0; y < _size; ++y)
            {
                for (int x = 0; x < _size; ++x)
                {
                    Color c = _src.GetPixel(x * _src.width / _size, y * _src.height / _size);
                    if (c.a <= 0f) { continue; }
                    Color d = _dst.GetPixel(_x + x, _y + y);
                    float a = c.a + d.a * (1f - c.a);
                    Color o = (c * c.a + d * d.a * (1f - c.a)) / Mathf.Max(a, 0.0001f);
                    o.a = a;
                    _dst.SetPixel(_x + x, _y + y, o);
                }
            }
        }

        #endregion

        #region Util

        // 테두리(광채) 스프라이트 뒤 + 본체 스프라이트 앞 2장으로 "합성 무기" 느낌. 레이어 Stage / 정렬 attack
        private static GameObject MakeVisual(string _name, string _sprite, Color _main, Color _glow, float _scale, float _glowRate, int _order, float _glowAlpha)
        {
            string path = PARTS + "/" + _name + ".prefab";
            EnsurePrefab(path, _name, null);
            EditPrefab(path, root =>
            {
                ClearChildren(root);
                root.transform.localScale = Vector3.one;
                MakeSprite(root.transform, "glow", _sprite, new Color(_glow.r, _glow.g, _glow.b, _glowAlpha), _scale * _glowRate, _order - 1);
                MakeSprite(root.transform, "body", _sprite, _main, _scale, _order);
                SetLayer(root);
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static SpriteRenderer MakeSprite(Transform _parent, string _name, string _sprite, Color _color, float _scale, int _order)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            go.transform.localScale = new Vector3(_scale, _scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite("Weapon/" + _sprite);
            sr.color = _color;
            sr.sortingLayerName = "attack";
            sr.sortingOrder = _order;
            return sr;
        }

        private static void ClearChildren(GameObject _root)
        {
            for (int i = _root.transform.childCount - 1; i >= 0; --i)
            {
                Object.DestroyImmediate(_root.transform.GetChild(i).gameObject);
            }
        }

        private static GameObject EnsurePrefab(string _path, string _name, System.Action<GameObject> _init)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            if (null != existing) { return existing; }
            var go = new GameObject(_name);
            _init?.Invoke(go);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, _path);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static void SetLayer(GameObject _root)
        {
            int layer = LayerMask.NameToLayer("Stage");
            if (layer < 0) { log.AppendLine("layer Stage missing"); return; }
            foreach (Transform t in _root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        private static Sprite Sprite(string _relative)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ART + "/" + _relative + ".png");
            if (null == s) { log.AppendLine("missing sprite " + _relative); }
            return s;
        }

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

        private static SerializedProperty Prop(Object _t, string _n, out SerializedObject _so)
        {
            _so = new SerializedObject(_t);
            SerializedProperty p = _so.FindProperty(_n);
            if (null == p) { log.AppendLine("missing prop " + _t.GetType().Name + "." + _n); }
            return p;
        }

        private static void SetObj(Object _t, string _n, Object _v)
        {
            var p = Prop(_t, _n, out var so);
            if (null == p) { return; }
            p.objectReferenceValue = _v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(Object _t, string _n, Color _v)
        {
            var p = Prop(_t, _n, out var so);
            if (null == p) { return; }
            p.colorValue = _v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object _t, string _n, float _v)
        {
            var p = Prop(_t, _n, out var so);
            if (null == p) { return; }
            p.floatValue = _v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object _t, string _n, int _v)
        {
            var p = Prop(_t, _n, out var so);
            if (null == p) { return; }
            p.intValue = _v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStr(Object _t, string _n, string _v)
        {
            var p = Prop(_t, _n, out var so);
            if (null == p) { return; }
            p.stringValue = _v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string _path)
        {
            if (AssetDatabase.IsValidFolder(_path)) { return; }
            string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
        }

        #endregion
    }
}
