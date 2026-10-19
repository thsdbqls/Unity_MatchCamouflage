using System.IO;
using MatchCamouflage.GameModes;
using MatchCamouflage.Jail;
using MatchCamouflage.Network;
using MatchCamouflage.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MatchCamouflage.EditorTools
{
    // README의 "프로젝트 열기" 수동 단계(씬/네트워크 매니저/플레이어 프리팹/감옥 구성)를
    // 에디터가 열려 있을 때 한 번 자동으로 실행한다. Main.scene이 이미 있으면 다시 돌지 않는다.
    [InitializeOnLoad]
    public static class ProjectSetupEditor
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string PrefabsListPath = "Assets/Prefabs/MainNetworkPrefabsList.asset";

        private const string BodyMaterialPath = "Assets/Prefabs/PlayerBodyMaterial.mat";
        private const string StickmanPrefabPath = "Assets/PolyOne/Free Stickman/Prefabs/Free Pack - Stick Man.prefab";
        private const string StickmanSwapMarkerPath = "Assets/Prefabs/.stickman-swap-done";
        private const string BootstrapMarkerPath = "Assets/Prefabs/.network-bootstrap-done";
        private const string ThirdPersonMarkerPath = "Assets/Prefabs/.third-person-camera-done";
        private const string TestLevelMarkerPath = "Assets/Prefabs/.test-level-done";
        private const string RayOriginFixMarkerPath = "Assets/Prefabs/.ray-origin-fix-done";
        private const string BigMapMarkerPath = "Assets/Prefabs/.big-map-done";
        private const string DenseWallsMarkerPath = "Assets/Prefabs/.dense-walls-done";
        private const string Resize100MarkerPath = "Assets/Prefabs/.resize-100-done";
        private const string FirstPersonMarkerPath = "Assets/Prefabs/.first-person-done";
        private const string CrosshairMarkerPath = "Assets/Prefabs/.crosshair-done";
        private const string AnimationDriverMarkerPath = "Assets/Prefabs/.animation-driver-done";

        static ProjectSetupEditor()
        {
            EditorApplication.delayCall += RunPendingTasks;
        }

        private static void RunPendingTasks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunPendingTasks; // 플레이 모드 끝나면 다시 시도
                return;
            }

            if (!File.Exists(ScenePath))
            {
                RunSetup();
                return;
            }

            if (!File.Exists(BodyMaterialPath) && File.Exists(PlayerPrefabPath))
            {
                FixBodyMaterial();
                return;
            }

            if (!File.Exists(StickmanSwapMarkerPath) && File.Exists(PlayerPrefabPath) && File.Exists(StickmanPrefabPath))
            {
                SwapToStickmanModel();
                return;
            }

            if (!File.Exists(BootstrapMarkerPath))
            {
                AddNetworkBootstrap();
                return;
            }

            if (!File.Exists(ThirdPersonMarkerPath) && File.Exists(PlayerPrefabPath))
            {
                SwitchToThirdPersonCamera();
                return;
            }

            if (!File.Exists(TestLevelMarkerPath))
            {
                BuildTestLevel();
                return;
            }

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                ActivateUrp();
                return;
            }

            if (!File.Exists(RayOriginFixMarkerPath) && File.Exists(PlayerPrefabPath))
            {
                FixRayOrigins();
                return;
            }

            if (!File.Exists(BigMapMarkerPath))
            {
                BuildBigMap();
                return;
            }

            if (!File.Exists(DenseWallsMarkerPath))
            {
                AddMoreHideWalls();
                return;
            }

            if (!File.Exists(Resize100MarkerPath))
            {
                ResizeMapTo100();
                return;
            }

            if (!File.Exists(FirstPersonMarkerPath) && File.Exists(PlayerPrefabPath))
            {
                SwitchToFirstPersonCamera();
                return;
            }

            if (!File.Exists(CrosshairMarkerPath) && File.Exists(PlayerPrefabPath))
            {
                AddCrosshair();
                return;
            }

            if (!File.Exists(AnimationDriverMarkerPath) && File.Exists(PlayerPrefabPath))
            {
                AddAnimationDriver();
            }
        }

        // Player 프리팹의 Stickman Animator를 찾아 PlayerAnimationDriver에 연결한다.
        private static void AddAnimationDriver()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            var animator = prefabRoot.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("[MatchCamouflage] Animator를 찾을 수 없어 애니메이션 연동을 건너뜁니다.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            var driver = prefabRoot.GetComponent<PlayerAnimationDriver>();
            if (driver == null)
            {
                driver = prefabRoot.AddComponent<PlayerAnimationDriver>();
            }

            var so = new SerializedObject(driver);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            File.WriteAllText(AnimationDriverMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 이동 속도에 따라 Idle/Walk/Run 애니메이션이 자동 재생되도록 연결했습니다.");
        }

        // Player 프리팹에 화면 중앙 조준점(PlayerCrosshair)을 붙인다.
        private static void AddCrosshair()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            if (prefabRoot.GetComponent<PlayerCrosshair>() == null)
            {
                prefabRoot.AddComponent<PlayerCrosshair>();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            File.WriteAllText(CrosshairMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 화면 중앙 조준점(PlayerCrosshair)을 추가했습니다.");
        }

        // 바닥을 100x100으로 줄이고, 기존 500x500 기준으로 흩뿌려둔 장애물을 전부 지운 뒤
        // 새 크기에 맞는 밀도로 다시 배치한다.
        private static void ResizeMapTo100()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var floor = GameObject.Find("Floor");
            if (floor != null)
            {
                floor.transform.localScale = new Vector3(10f, 1f, 10f); // 기본 Plane 10x10 -> 100x100
            }

            var wallsRoot = GameObject.Find("Walls");
            if (wallsRoot != null)
            {
                Object.DestroyImmediate(wallsRoot);
            }
            wallsRoot = new GameObject("Walls");

            var paletteMaterials = new System.Collections.Generic.List<Material>();
            for (int i = 0; i < 8; i++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Prefabs/Level_WallPalette{i}.mat");
                if (mat != null) paletteMaterials.Add(mat);
            }

            Random.InitState(4242);
            const int wallCount = 110;
            const float mapHalfExtent = 42f; // 100x100 바닥 안쪽
            const float clearRadius = 8f;

            for (int i = 0; i < wallCount; i++)
            {
                Vector3 pos;
                do
                {
                    pos = new Vector3(
                        Random.Range(-mapHalfExtent, mapHalfExtent),
                        0f,
                        Random.Range(-mapHalfExtent, mapHalfExtent));
                } while (pos.sqrMagnitude < clearRadius * clearRadius);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"HideWall_{i}";
                wall.transform.SetParent(wallsRoot.transform);

                bool isPillar = i % 2 == 0;
                float width = isPillar ? Random.Range(1f, 2f) : Random.Range(2f, 5f);
                float height = Random.Range(2.5f, 5f);
                float depth = isPillar ? Random.Range(1f, 2f) : Random.Range(0.5f, 1f);
                wall.transform.localScale = new Vector3(width, height, depth);
                wall.transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
                wall.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);

                if (paletteMaterials.Count > 0)
                {
                    var renderer = wall.GetComponent<Renderer>();
                    renderer.sharedMaterial = paletteMaterials[Random.Range(0, paletteMaterials.Count)];
                }
                wall.AddComponent<Paintable>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            File.WriteAllText(Resize100MarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log($"[MatchCamouflage] 바닥을 100x100으로 줄이고 장애물 {wallCount}개를 새 크기에 맞게 다시 배치했습니다.");
        }

        // 3인칭 오빗 카메라를 다시 1인칭(머리 위치) 카메라로 되돌린다.
        private static void SwitchToFirstPersonCamera()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            var cameraTransform = prefabRoot.transform.Find("CameraPivot/Camera");
            if (cameraTransform == null)
            {
                Debug.LogWarning("[MatchCamouflage] CameraPivot/Camera 를 찾을 수 없어 1인칭 전환을 건너뜁니다.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            cameraTransform.localPosition = Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            File.WriteAllText(FirstPersonMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 카메라를 1인칭으로 되돌렸습니다.");
        }

        // 숨을 곳이 부족하다는 피드백으로 장애물을 대량 추가 배치한다(기존 Walls 위에 덧붙임).
        private static void AddMoreHideWalls()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var wallsRoot = GameObject.Find("Walls");
            if (wallsRoot == null)
            {
                wallsRoot = new GameObject("Walls");
            }

            var paletteMaterials = new System.Collections.Generic.List<Material>();
            for (int i = 0; i < 8; i++)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Prefabs/Level_WallPalette{i}.mat");
                if (mat != null) paletteMaterials.Add(mat);
            }

            Random.InitState(9001);
            const int extraWallCount = 350;
            const float mapHalfExtent = 240f;
            const float clearRadius = 15f;

            for (int i = 0; i < extraWallCount; i++)
            {
                Vector3 pos;
                do
                {
                    pos = new Vector3(
                        Random.Range(-mapHalfExtent, mapHalfExtent),
                        0f,
                        Random.Range(-mapHalfExtent, mapHalfExtent));
                } while (pos.sqrMagnitude < clearRadius * clearRadius);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"HideWallExtra_{i}";
                wall.transform.SetParent(wallsRoot.transform);

                // 가로로 긴 벽 절반, 기둥처럼 좁고 두꺼운 장애물 절반 - 숨을 곳 모양을 다양하게
                bool isPillar = i % 2 == 0;
                float width = isPillar ? Random.Range(1f, 2.5f) : Random.Range(2.5f, 7f);
                float height = Random.Range(2.5f, 5f);
                float depth = isPillar ? Random.Range(1f, 2.5f) : Random.Range(0.5f, 1.2f);
                wall.transform.localScale = new Vector3(width, height, depth);
                wall.transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
                wall.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);

                if (paletteMaterials.Count > 0)
                {
                    var renderer = wall.GetComponent<Renderer>();
                    renderer.sharedMaterial = paletteMaterials[Random.Range(0, paletteMaterials.Count)];
                }
                wall.AddComponent<Paintable>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            File.WriteAllText(DenseWallsMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log($"[MatchCamouflage] 숨을 곳 {extraWallCount}개를 추가로 배치해 맵을 더 촘촘하게 만들었습니다.");
        }

        // 작은 4벽 테스트장을 지우고 500x500 맵 위에 숨을 수 있는 벽을 무작위로 흩뿌린다.
        private static void BuildBigMap()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

            // 기존 작은 테스트 벽 제거
            foreach (var wallName in new[] { "Wall_Red", "Wall_Green", "Wall_Blue", "Wall_Yellow" })
            {
                var old = GameObject.Find(wallName);
                if (old != null) Object.DestroyImmediate(old);
            }

            // 바닥을 500x500으로 확장 (기본 Plane은 10x10이므로 스케일 50)
            var floor = GameObject.Find("Floor");
            if (floor != null)
            {
                floor.transform.localScale = new Vector3(50f, 1f, 50f);
            }

            // 위장용 팔레트 - 채도 낮은 자연스러운 색 8종, 재사용 가능한 머티리얼로 미리 생성
            var palette = new[]
            {
                new Color(0.55f, 0.55f, 0.55f), // 회색
                new Color(0.35f, 0.35f, 0.38f), // 짙은 회색
                new Color(0.62f, 0.56f, 0.45f), // 베이지
                new Color(0.33f, 0.42f, 0.30f), // 짙은 녹색
                new Color(0.47f, 0.33f, 0.24f), // 갈색
                new Color(0.70f, 0.68f, 0.60f), // 연한 모래색
                new Color(0.25f, 0.28f, 0.32f), // 슬레이트
                new Color(0.60f, 0.40f, 0.35f), // 붉은 벽돌색
            };
            var paletteMaterials = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                string path = $"Assets/Prefabs/Level_WallPalette{i}.mat";
                if (!File.Exists(path) && urpLitShader != null)
                {
                    AssetDatabase.CreateAsset(new Material(urpLitShader) { color = palette[i] }, path);
                }
                paletteMaterials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            var wallsRoot = new GameObject("Walls");

            Random.InitState(2026);
            const int wallCount = 160;
            const float mapHalfExtent = 240f; // 500보다 살짝 안쪽까지만 배치해 가장자리 여유를 둠
            const float clearRadius = 15f;    // 스폰 지점 근처는 비워둠

            for (int i = 0; i < wallCount; i++)
            {
                Vector3 pos;
                do
                {
                    pos = new Vector3(
                        Random.Range(-mapHalfExtent, mapHalfExtent),
                        0f,
                        Random.Range(-mapHalfExtent, mapHalfExtent));
                } while (pos.sqrMagnitude < clearRadius * clearRadius);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"HideWall_{i}";
                wall.transform.SetParent(wallsRoot.transform);

                float width = Random.Range(3f, 9f);
                float height = Random.Range(3f, 5f);
                float depth = Random.Range(0.5f, 1.2f);
                wall.transform.localScale = new Vector3(width, height, depth);
                wall.transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
                wall.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);

                var renderer = wall.GetComponent<Renderer>();
                var material = paletteMaterials[Random.Range(0, paletteMaterials.Length)];
                if (renderer != null && material != null)
                {
                    renderer.sharedMaterial = material;
                }
                wall.AddComponent<Paintable>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            File.WriteAllText(BigMapMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log($"[MatchCamouflage] 500x500 맵으로 확장하고 숨을 수 있는 벽 {wallCount}개를 무작위 배치했습니다.");
        }

        // 3인칭 전환으로 카메라가 캐릭터보다 한참 뒤로 빠지면서, 칠하기/사격 레이가 카메라 위치에서
        // 시작하는 바람에 사거리 안에 벽이 안 들어오게 됐다. 레이 시작점을 캐릭터 머리(CameraPivot)로 옮긴다.
        private static void FixRayOrigins()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            var cameraPivot = prefabRoot.transform.Find("CameraPivot");
            if (cameraPivot == null)
            {
                Debug.LogWarning("[MatchCamouflage] CameraPivot을 찾을 수 없어 레이 시작점 보정을 건너뜁니다.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            var paint = prefabRoot.GetComponent<PlayerPaint>();
            if (paint != null)
            {
                var paintSO = new SerializedObject(paint);
                paintSO.FindProperty("paintRayOrigin").objectReferenceValue = cameraPivot;
                paintSO.ApplyModifiedPropertiesWithoutUndo();
            }

            var shooter = prefabRoot.GetComponent<PlayerShooter>();
            if (shooter != null)
            {
                var shooterSO = new SerializedObject(shooter);
                shooterSO.FindProperty("muzzle").objectReferenceValue = cameraPivot;
                shooterSO.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            File.WriteAllText(RayOriginFixMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 칠하기/사격 레이 시작점을 카메라에서 캐릭터 머리(CameraPivot)로 옮겼습니다.");
        }

        // URP 패키지만 설치되어 있고 실제 파이프라인 에셋이 활성화되어 있지 않으면
        // URP Lit 셰이더를 쓰는 모든 머티리얼이 핑크(렌더 파이프라인 불일치)로 보인다.
        private static void ActivateUrp()
        {
            Directory.CreateDirectory("Assets/Settings");

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);

            var urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urpAsset, "Assets/Settings/URP_Asset.asset");

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] URP 파이프라인 에셋을 생성하고 활성화했습니다(Assets/Settings/URP_Asset.asset). 이제 핑크 머티리얼이 정상 색으로 보여야 합니다.");
        }

        // 페인트 테스트용으로 바닥 + 색이 다른 벽 4개를 배치한다. 전부 Paintable 포함.
        private static void BuildTestLevel()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 3f); // 10x10 기본 플레인 -> 30x30
            ApplyUnlitColorMaterial(floor, urpLitShader, new Color(0.75f, 0.75f, 0.75f), "Assets/Prefabs/Level_FloorMaterial.mat");
            floor.AddComponent<Paintable>();

            CreateWall("Wall_Red", new Vector3(8f, 1.5f, 0f), Quaternion.identity, Color.red, urpLitShader);
            CreateWall("Wall_Green", new Vector3(-8f, 1.5f, 0f), Quaternion.identity, Color.green, urpLitShader);
            CreateWall("Wall_Blue", new Vector3(0f, 1.5f, 8f), Quaternion.Euler(0f, 90f, 0f), Color.blue, urpLitShader);
            CreateWall("Wall_Yellow", new Vector3(0f, 1.5f, -8f), Quaternion.Euler(0f, 90f, 0f), Color.yellow, urpLitShader);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            File.WriteAllText(TestLevelMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 페인트 테스트용 바닥 + 빨강/초록/파랑/노랑 벽 4개를 배치했습니다. 벽 앞에서 F를 누르고 있으면 그 색으로 칠해집니다.");
        }

        private static void CreateWall(string name, Vector3 position, Quaternion rotation, Color color, Shader urpLitShader)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.rotation = rotation;
            wall.transform.localScale = new Vector3(6f, 3f, 0.3f);
            ApplyUnlitColorMaterial(wall, urpLitShader, color, $"Assets/Prefabs/Level_{name}Material.mat");
            wall.AddComponent<Paintable>();
        }

        private static void ApplyUnlitColorMaterial(GameObject target, Shader urpLitShader, Color color, string assetPath)
        {
            if (urpLitShader == null) return;
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;

            var material = new Material(urpLitShader) { color = color };
            if (!File.Exists(assetPath))
            {
                AssetDatabase.CreateAsset(material, assetPath);
            }
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }

        // 1인칭(머리 속) 카메라를 캐릭터 뒤쪽 위에서 바라보는 3인칭 오빗 카메라로 바꾼다.
        // 위장(페인트) 메커닉 특성상 본인 몸이 보여야 의미가 있어서 3인칭이 더 적합하다.
        private static void SwitchToThirdPersonCamera()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            var cameraTransform = prefabRoot.transform.Find("CameraPivot/Camera");
            if (cameraTransform == null)
            {
                Debug.LogWarning("[MatchCamouflage] CameraPivot/Camera 를 찾을 수 없어 3인칭 전환을 건너뜁니다.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            cameraTransform.localPosition = new Vector3(0f, 0.6f, -4f);
            cameraTransform.localRotation = Quaternion.Euler(8f, 0f, 0f); // 살짝 아래를 보게

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            File.WriteAllText(ThirdPersonMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 카메라를 3인칭 오빗 카메라로 전환했습니다.");
        }

        // Host/Client/Server를 누를 수 있는 임시 테스트 UI를 NetworkManager 오브젝트에 붙인다.
        private static void AddNetworkBootstrap()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var nmGO = GameObject.Find("NetworkManager");
            if (nmGO == null)
            {
                Debug.LogWarning("[MatchCamouflage] NetworkManager 오브젝트를 찾을 수 없어 부트스트랩 UI 추가를 건너뜁니다.");
                return;
            }

            if (nmGO.GetComponent<NetworkBootstrap>() == null)
            {
                nmGO.AddComponent<NetworkBootstrap>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            File.WriteAllText(BootstrapMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] NetworkManager에 Host/Client/Server 테스트 버튼(NetworkBootstrap)을 추가했습니다. Play 후 화면 좌상단 Host 버튼을 누르세요.");
        }

        // Body(캡슐 프리미티브)를 에셋 스토어에서 받은 Free Pack - Stick Man 모델로 교체한다.
        private static void SwapToStickmanModel()
        {
            var stickmanSource = AssetDatabase.LoadAssetAtPath<GameObject>(StickmanPrefabPath);
            if (stickmanSource == null)
            {
                Debug.LogWarning($"[MatchCamouflage] {StickmanPrefabPath} 를 찾을 수 없어 모델 교체를 건너뜁니다.");
                return;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            var oldBody = prefabRoot.transform.Find("Body");
            if (oldBody != null)
            {
                Object.DestroyImmediate(oldBody.gameObject);
            }

            var stickmanInstance = (GameObject)PrefabUtility.InstantiatePrefab(stickmanSource, prefabRoot.transform);
            stickmanInstance.name = "Body";
            stickmanInstance.transform.localPosition = Vector3.zero;
            stickmanInstance.transform.localRotation = Quaternion.identity;

            // Animator가 자체적으로 위치를 밀지 않도록 한다. 이동은 CharacterController(PlayerMovement)가 전담한다.
            var animator = stickmanInstance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            var bodyRenderer = stickmanInstance.GetComponentInChildren<SkinnedMeshRenderer>();
            var paint = prefabRoot.GetComponent<PlayerPaint>();
            if (bodyRenderer != null && paint != null)
            {
                var paintSO = new SerializedObject(paint);
                paintSO.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
                paintSO.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            File.WriteAllText(StickmanSwapMarkerPath, "done");
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] Player 프리팹의 Body를 Free Pack - Stick Man 모델로 교체했습니다.");
        }

        // RunSetup이 URP 머티리얼 보정 코드 추가 전에 이미 한 번 실행된 경우를 위한 1회성 보정.
        private static void FixBodyMaterial()
        {
            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null) return;

            var bodyTransform = prefab.transform.Find("Body");
            var bodyRenderer = bodyTransform != null ? bodyTransform.GetComponent<Renderer>() : null;
            if (bodyRenderer == null) return;

            var bodyMaterial = new Material(urpLitShader) { color = Color.white };
            AssetDatabase.CreateAsset(bodyMaterial, BodyMaterialPath);

            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            prefabRoot.transform.Find("Body").GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] Player 프리팹 Body에 URP Lit 머티리얼을 적용했습니다.");
        }

        [MenuItem("MatchCamouflage/Run Initial Setup")]
        private static void RunSetup()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log("[MatchCamouflage] 이미 초기 설정이 완료되어 있습니다.");
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject playerPrefabAsset = BuildPlayerPrefab();
            GameObject jailZoneGO = BuildJailZone();
            BuildNetworkManager(playerPrefabAsset);
            BuildGameModeManager(jailZoneGO);

            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = EditorBuildSettings.scenes;
            bool alreadyInBuild = System.Array.Exists(buildScenes, s => s.path == ScenePath);
            if (!alreadyInBuild)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(buildScenes)
                {
                    new EditorBuildSettingsScene(ScenePath, true)
                };
                EditorBuildSettings.scenes = list.ToArray();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MatchCamouflage] 초기 설정 완료: Main.scene / Player 프리팹 / NetworkManager / GameModeManager / JailZone 구성됨.");
        }

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);

            root.AddComponent<NetworkObject>();

            var networkTransform = root.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            networkTransform.AuthorityMode = Unity.Netcode.Components.NetworkTransform.AuthorityModes.Owner;

            root.AddComponent<PlayerState>();
            var movement = root.AddComponent<PlayerMovement>();
            var paint = root.AddComponent<PlayerPaint>();
            var shooter = root.AddComponent<PlayerShooter>();

            // 몸체 시각화 (충돌체는 CharacterController만 사용하므로 자동 생성된 CapsuleCollider는 제거)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            var bodyRenderer = body.GetComponent<Renderer>();

            // CreatePrimitive 기본 머티리얼은 Built-in Standard 셰이더라 URP에서 핑크로 보인다.
            // PlayerPaint가 "_BaseColor"(URP Lit)로 색을 입히므로 URP Lit 머티리얼을 만들어 붙여준다.
            var urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader != null)
            {
                var bodyMaterial = new Material(urpLitShader) { color = Color.white };
                AssetDatabase.CreateAsset(bodyMaterial, "Assets/Prefabs/PlayerBodyMaterial.mat");
                bodyRenderer.sharedMaterial = bodyMaterial;
            }

            var cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(root.transform);
            cameraPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraGO = new GameObject("Camera");
            cameraGO.transform.SetParent(cameraPivot.transform);
            cameraGO.transform.localPosition = Vector3.zero;
            cameraGO.AddComponent<Camera>();
            cameraGO.AddComponent<AudioListener>();

            // private [SerializeField] 참조는 SerializedObject로 연결한다.
            var moveSO = new SerializedObject(movement);
            moveSO.FindProperty("cameraPivot").objectReferenceValue = cameraPivot.transform;
            moveSO.ApplyModifiedPropertiesWithoutUndo();

            var paintSO = new SerializedObject(paint);
            paintSO.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
            paintSO.FindProperty("paintRayOrigin").objectReferenceValue = cameraGO.transform;
            paintSO.ApplyModifiedPropertiesWithoutUndo();

            var shooterSO = new SerializedObject(shooter);
            shooterSO.FindProperty("muzzle").objectReferenceValue = cameraGO.transform;
            shooterSO.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        }

        private static GameObject BuildJailZone()
        {
            var jailGO = new GameObject("JailZone");
            jailGO.transform.position = new Vector3(20f, 0f, 20f);
            var collider = jailGO.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(5f, 3f, 5f);
            var jailZone = jailGO.AddComponent<JailZone>();

            var cellPoints = new Transform[3];
            for (int i = 0; i < cellPoints.Length; i++)
            {
                var cell = new GameObject($"CellPoint_{i}");
                cell.transform.SetParent(jailGO.transform);
                cell.transform.localPosition = new Vector3(i * 1.5f - 1.5f, 0f, 0f);
                cellPoints[i] = cell.transform;
            }

            var so = new SerializedObject(jailZone);
            var cellPointsProp = so.FindProperty("cellPoints");
            cellPointsProp.arraySize = cellPoints.Length;
            for (int i = 0; i < cellPoints.Length; i++)
            {
                cellPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = cellPoints[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return jailGO;
        }

        private static void BuildNetworkManager(GameObject playerPrefabAsset)
        {
            var nmGO = new GameObject("NetworkManager");
            var networkManager = nmGO.AddComponent<NetworkManager>();
            var transport = nmGO.AddComponent<UnityTransport>();

            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefabAsset
            };

            var prefabsList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabsList.Add(new NetworkPrefab { Prefab = playerPrefabAsset });
            AssetDatabase.CreateAsset(prefabsList, PrefabsListPath);
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);
        }

        private static void BuildGameModeManager(GameObject jailZoneGO)
        {
            var gmGO = new GameObject("GameModeManager");
            var gameModeManager = gmGO.AddComponent<GameModeManager>();

            var so = new SerializedObject(gameModeManager);
            so.FindProperty("jailZone").objectReferenceValue = jailZoneGO.GetComponent<JailZone>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
