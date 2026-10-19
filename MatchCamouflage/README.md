# Match Camouflage

맷치 카멜레온류 하이드앤시크 게임. Unity 6000.0.32f1 + Netcode for GameObjects(온라인 멀티플레이어) 기반.

## 모드

1. **Manhunt (경찰과 도둑)**: Seeker(총 보유, 1명 이상)가 Hider(페인트로 벽과 같은 색을 칠해 위장)를 찾아 쏘면 피격당한 Hider는 감옥으로 이동. 투옥되지 않은 다른 Hider가 감옥 근처로 가면 투옥된 인원 전원 석방. 3회 피격되면 완전 탈락(관전).
2. **Team Deathmatch**: 2팀으로 분할, 페인트로 위장하며 이동, 상대팀을 맞추면 즉시 탈락. 한 팀이 전멸하면 라운드 종료.

## 코드 구조

```
Assets/Scripts/
  Player/
    PlayerState.cs      역할/스트라이크/투옥/탈락/몸색 NetworkVariable 보관 (쓰기 권한은 전부 서버, BodyColor만 오너)
    PlayerMovement.cs   오너 권위 이동 (CharacterController, WASD + 마우스룩)
    PlayerPaint.cs       Paintable 표면을 바라보며 F키로 색칠 → BodyColor 보간
    Paintable.cs         칠할 수 있는 표면에 붙이는 마커 (렌더러 색 또는 지정 색)
    PlayerShooter.cs     좌클릭 발사 → ServerRpc로 서버가 레이캐스트 판정 (치트 방지)
  Jail/
    JailZone.cs           투옥 지점 보관 + 반경 내 비투옥 Hider 감지 시 전원 석방
  GameModes/
    GameModeManager.cs   역할/팀 배정, 피격 판정 분기(Manhunt vs TeamDeathmatch), 승리 조건 체크
```

판정 흐름: `PlayerShooter`(클라이언트 입력) → `ServerRpc` → 서버에서 레이캐스트 → `GameModeManager.ServerResolveHit` → 모드별 분기(투옥 또는 즉시 탈락) → `PlayerState` NetworkVariable 갱신 → 모든 클라이언트에 자동 동기화.

## 프로젝트 열기 (최초 1회, 에디터 GUI 작업 필요)

커맨드라인에서는 Unity 라이선스 인증을 할 수 없어서(Hub 로그인 필요) 프로젝트 생성/패키지 설치까지만 자동화했습니다. 아래는 Unity Hub + 에디터에서 직접 해야 하는 작업입니다.

1. **Unity Hub에서 프로젝트 추가**: Hub → Projects → Add → `E:\MatchCamouflage` 선택 → 6000.0.32f1로 열기.
2. **Netcode 패키지 설치**: Window → Package Manager → 상단 드롭다운을 "Unity Registry"로 변경 →
   - `Netcode for GameObjects` 설치
   - `Multiplayer Tools` 설치 (선택, 네트워크 프로파일러용)
   - Unity Transport는 Netcode for GameObjects 설치 시 자동으로 같이 들어옵니다.
3. **렌더 파이프라인**: manifest.json에 URP(`com.unity.render-pipelines.universal`)를 미리 넣어뒀습니다. 처음 열 때 "업그레이드할까요?" 프롬프트가 뜨면 수락하거나, Edit → Project Settings → Graphics에서 URP Asset을 수동으로 연결하세요.
4. **씬 구성**:
   - `Assets/Scenes/Main.scene` 생성
   - 빈 GameObject에 `NetworkManager`(Netcode 패키지 제공) 컴포넌트 추가 → Transport로 `Unity Transport` 선택
   - 같은 오브젝트나 별도 오브젝트에 `GameModeManager` 컴포넌트 추가, Inspector에서 `Selected Mode`(Manhunt/TeamDeathmatch), `Seeker Count`, `Jail Zone` 참조 지정
   - 감옥 위치에 빈 GameObject + Collider(Is Trigger 불필요, OverlapSphere 사용) + `JailZone` 컴포넌트, `Cell Points`에 투옥 대기 지점들 연결
5. **플레이어 프리팹**:
   - Capsule 등으로 모델 구성 → `CharacterController`, `NetworkObject`, `NetworkTransform`(Owner Authoritative), `PlayerState`, `PlayerMovement`, `PlayerPaint`, `PlayerShooter` 컴포넌트 추가
   - `PlayerMovement`의 `Camera Pivot`에 카메라 자식 오브젝트 연결, `PlayerPaint`의 `Body Renderer`에 몸체 Renderer 연결
   - `Assets`에 프리팹으로 저장 → `NetworkManager`의 `NetworkPrefabs` 리스트 및 `Player Prefab`에 등록
6. **칠할 수 있는 벽/바닥**: 레벨의 벽·바닥 오브젝트에 `Paintable` 컴포넌트 추가 (기본값은 자기 머티리얼 색을 그대로 사용).
7. **빌드 세팅**: File → Build Settings → Scenes In Build에 `Main.scene` 추가.

## 조작

- 이동: WASD, 마우스룩, Shift 스프린트
- 위장(칠하기): F (Paintable 표면을 바라본 채로 유지)
- 발사: 좌클릭 (Seeker/상대팀만 유효 판정, 서버가 역할 검증)

## 알려진 미구현 영역 (확장 포인트)

- 라운드 타이머 / 로비·모드 선택 UI
- 위장 중 정지 시 Seeker 탐지 반경 감소 같은 "진짜 카멜레온" 효과
- 머즐 플래시/피격 이펙트, 사운드
- 관전 모드 UI (현재는 `IsEliminated`만 서버에서 true가 되고 이동이 막힐 뿐, 카메라 전환 등은 미구현)
