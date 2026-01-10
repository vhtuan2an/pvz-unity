# CHƯƠNG 3: CÀI ĐẶT VÀ XÂY DỰNG

## 3.1. CẤU TRÚC PROJECT TRONG UNITY

### 3.1.1. Tổng quan cấu trúc

```
📁 Assets/
├── 📁 Animations/           # 260 animation clips & controllers
├── 📁 Prefabs/              # Tất cả game objects
│   ├── 📁 Plants/           # 16 plant prefabs
│   ├── 📁 Zombies/          # 10 zombie prefabs
│   ├── 📁 Managers/         # Singleton managers
│   ├── 📁 UI/               # UI components
│   └── 📁 Utilities/        # Projectiles, effects
├── 📁 Scenes/               # 5 Unity scenes
├── 📁 Scripts/              # 56 C# source files
│   ├── 📁 Camera/           # Camera controllers
│   ├── 📁 Networking/       # 12 network scripts
│   ├── 📁 Plants/           # 11 plant behaviors
│   ├── 📁 Players/          # Player data
│   ├── 📁 UI/               # 4 UI scripts
│   ├── 📁 Utilities/        # 42+ utility scripts
│   └── 📁 Zombies/          # 7 zombie behaviors
├── 📁 Sprites/              # 10,000+ sprite files
├── 📁 Resources/            # Runtime loadable assets
├── 📁 PlayFabSDK/           # PlayFab integration
└── 📁 TextMesh Pro/         # UI text rendering
```

### 3.1.2. Phân tầng kiến trúc

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                        │
│   LoginUI.cs | LobbyUI.cs | SelectionUI.cs | UIManager.cs       │
├─────────────────────────────────────────────────────────────────┤
│                         GAME LOGIC LAYER                         │
│   PlantManager.cs | ZombieManager.cs | FusionManager.cs         │
│   PlantBase.cs | ZombieBase.cs | Peashooter.cs | BasicZombie.cs │
├─────────────────────────────────────────────────────────────────┤
│                        NETWORKING LAYER                          │
│   NetworkGameManager.cs | GameStateManager.cs | LobbyManager.cs │
│   UnityAuthManager.cs | LoadingSceneManager.cs                  │
├─────────────────────────────────────────────────────────────────┤
│                         UTILITY LAYER                            │
│   SoundManager.cs | Tile.cs | SunSpawner.cs | BrainSpawner.cs   │
└─────────────────────────────────────────────────────────────────┘
```

### 3.1.3. Prefab Organization

| Thư mục             | Nội dung                             | Số lượng |
| ------------------- | ------------------------------------ | -------- |
| `Prefabs/Plants/`   | Peashooter, Sunflower, Wallnut, v.v. | 16       |
| `Prefabs/Zombies/`  | BasicZombie, Allstar, Cannon, v.v.   | 10       |
| `Prefabs/UI/`       | SeedPacket, ZombiePacket, Dialogs    | 10+      |
| `Prefabs/Managers/` | NetworkGameManager, SoundManager     | 5        |

---

## 3.2. CÀI ĐẶT CÁC CHỨC NĂNG CHÍNH

### 3.2.1. Hệ thống Authentication

**File**: `Scripts/Networking/UnityAuthManager.cs`

```csharp
// Singleton pattern cho Authentication
public class UnityAuthManager : MonoBehaviour
{
    public static UnityAuthManager Instance { get; private set; }

    public async Task SignInAnonymously()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // PlayFab integration
        PlayFabClientAPI.LoginWithCustomID(...);
    }
}
```

**Luồng hoạt động:**

```
User nhập Username → Unity Auth SignIn → PlayFab Login → Lưu Player Data
```

### 3.2.2. Hệ thống Lobby & Matchmaking

**File**: `Scripts/Networking/LobbyManager.cs` (704 dòng)

```csharp
public class LobbyManager : MonoBehaviour
{
    // Singleton
    public static LobbyManager Instance { get; private set; }

    // Properties
    public PlayerRole SelectedRole { get; private set; }
    public Lobby CurrentLobby { get; private set; }

    // Events
    public event Action<PlayerRole> OnRoleSelected;
    public event Action<string> OnMatchFound;

    // Heartbeat với exponential backoff
    private float lobbyHeartbeatInterval = 1.5f;
    private float minPollInterval = 10f;
    private float maxPollInterval = 30f;
}
```

**Chức năng chính:**

| Method             | Mô tả                              |
| ------------------ | ---------------------------------- |
| `SelectRole()`     | Chọn phe Plant/Zombie              |
| `CreateLobby()`    | Tạo lobby mới với Relay allocation |
| `JoinLobby()`      | Join lobby có sẵn                  |
| `LeaveLobby()`     | Rời khỏi lobby                     |
| `StartHeartbeat()` | Duy trì lobby connection           |

### 3.2.3. Hệ thống Network Game

**File**: `Scripts/Networking/NetworkGameManager.cs` (299 dòng)

```csharp
public class NetworkGameManager : NetworkBehaviour
{
    [System.Serializable]
    public class PlantPrefabMapping
    {
        public string plantName;
        public NetworkObject prefab;
    }

    // Spawn Plants (Server-authoritative)
    public void SpawnPlantAtPosition(Vector3 position, string plantName)
    {
        if (!IsServer) return;
        RequestSpawnPlantServerRpc(position, plantName, clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPlantServerRpc(Vector3 pos, string name, ulong clientId)
    {
        // Validate và spawn
        NetworkObject plantInstance = Instantiate(prefab, pos, rotation);
        plantInstance.Spawn(true);
        NotifyPlantSpawnedClientRpc(plantInstance.NetworkObjectId);
    }
}
```

### 3.2.4. Hệ thống Plant Management

**File**: `Scripts/Utilities/Plant/PlantManager.cs` (619 dòng)

**Chức năng:**

```csharp
public class PlantManager : MonoBehaviour
{
    // UI References
    [SerializeField] private TextMeshProUGUI sunCounterText;
    [SerializeField] private Sprite shovelSprite;

    // State
    private int currentSun = 50;
    private GameObject selectedPlant;
    private bool isShovelMode = false;

    // Preview system
    private SpriteRenderer previewRenderer;

    // Core Methods
    public void SelectPlant(GameObject plantPrefab) { ... }
    public void TryPlaceOnTile(Tile tile) { ... }
    public void ToggleShovel() { ... }
    public void AddSun(int amount) { ... }
}
```

**Luồng đặt cây:**

```
SelectPlant() → Update Preview → Click Tile → CheckSun → CheckFusion → SpawnPlant
```

### 3.2.5. Hệ thống Fusion

**File**: `Scripts/Utilities/Plant/FusionManager.cs`

```csharp
public class FusionManager : MonoBehaviour
{
    [SerializeField] private List<FusionRecipe> fusionRecipes;

    public bool TryFusion(Tile tile, GameObject existingPlant, GameObject plantToPlace)
    {
        // Wallnut First Aid
        if (existingPlant.name.Contains("Wallnut") &&
            plantToPlace.name.Contains("Wallnut"))
        {
            Wallnut wallnut = existingPlant.GetComponent<Wallnut>();
            if (wallnut.CurrentHealth < wallnut.MaxHealth)
            {
                wallnut.RestoreHealth();
                return true;
            }
        }

        // Regular fusion
        FusionRecipe recipe = GetFusionRecipe(existingPlant, plantToPlace);
        if (recipe != null)
        {
            tile.Clear();
            NetworkGameManager.Instance.SpawnPlantAtPosition(
                tile.PlantWorldPosition,
                recipe.resultFusion.name
            );
            return true;
        }
        return false;
    }
}
```

---

## 3.3. AI, ANIMATION, AUDIO

### 3.3.1. AI System (Zombie Behavior)

**Nguyên lý:** Không sử dụng Unity NavMesh, thay vào đó dùng **Physics2D.BoxCast** để đơn giản hóa pathfinding trong game 2D lane-based.

**File**: `Scripts/Zombies/BasicZombie.cs`

```csharp
private void FixedUpdate()
{
    if (!IsServer) return;

    // BoxCast để phát hiện Plant phía trước
    RaycastHit2D hit = Physics2D.BoxCast(
        rb.position,
        boxCollider.size,
        0f,
        Vector2.left,
        checkDistance,
        LayerMask.GetMask("Plant")
    );

    if (hit.collider == null)
    {
        // Không gặp cây → Di chuyển
        rb.MovePosition(rb.position + movement);
        SetWalkingClientRpc(true);
        SetEatingClientRpc(false);
    }
    else
    {
        // Gặp cây → Tấn công
        SetWalkingClientRpc(false);
        SetEatingClientRpc(true);

        if (attackTimer >= attackRate)
        {
            PlantBase plant = hit.collider.GetComponent<PlantBase>();
            plant?.TakeDamage(GetDamage());
            attackTimer = 0f;
        }
    }
}
```

**AI States:**

```
┌──────────────────────────────────────────────────────────────┐
│                        ZOMBIE AI FSM                          │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│   ┌─────────┐  No Plant   ┌─────────┐  Plant Found  ┌──────┐ │
│   │  IDLE   │ ──────────► │ WALKING │ ────────────► │EATING│ │
│   └─────────┘             └─────────┘               └──────┘ │
│        ▲                       ▲                        │     │
│        │                       │  Plant Destroyed       │     │
│        │                       └────────────────────────┘     │
│        │                                                      │
│        │                  ┌─────────┐                         │
│        └──────────────────│  DEAD   │ (HP <= 0)               │
│                           └─────────┘                         │
└──────────────────────────────────────────────────────────────┘
```

### 3.3.2. Animation System

**Cấu trúc:** Sử dụng Unity Animator với **Animation Events** để trigger logic.

**Ví dụ Peashooter:**

```csharp
// Scripts/Plants/Peashooter.cs
public class Peashooter : PlantBase
{
    private Animator animator;

    private void TriggerShoot()
    {
        isShooting = true;
        TriggerShootAnimationClientRpc();  // Sync animation
    }

    // Called by Animation Event
    private void SpawnPea()
    {
        if (!IsServer) return;
        StartCoroutine(ShootBurst());
    }

    [ClientRpc]
    private void TriggerShootAnimationClientRpc()
    {
        animator.SetBool("isShooting", true);
    }
}
```

**Animator Parameters:**

| Plant/Zombie | Parameters                         |
| ------------ | ---------------------------------- |
| Peashooter   | `isShooting`, `Hit`                |
| Sunflower    | `isProducing`, `Blink`             |
| Wallnut      | `Degrade1`, `Degrade2`, `Degrade3` |
| BasicZombie  | `isWalking`, `isEating`, `Die`     |

**Thống kê Animation:**

| Loại              | Số lượng |
| ----------------- | -------- |
| Plant Animations  | ~100     |
| Zombie Animations | ~150     |
| UI Animations     | ~10      |
| **Tổng**          | **260+** |

### 3.3.3. Audio System

**File**: `Scripts/Utilities/SoundManager.cs` (171 dòng)

**Kỹ thuật:** **Object Pooling** cho AudioSource

```csharp
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private int initialPoolSize = 10;
    private Queue<AudioSource> audioSourcePool;
    private Dictionary<string, AudioClip[]> clipCache;

    public void PlaySound(string clipName, float volume = 1f, float pitch = 1f)
    {
        AudioClip[] clips = GetClips(clipName);
        AudioClip selectedClip = clips[Random.Range(0, clips.Length)];
        PlayClip(selectedClip, volume, pitch);
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        AudioSource source = GetAudioSource();  // From pool
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
        ReturnToPool(source, clip.length / pitch + 0.1f);
    }
}
```

**Danh sách Sound Effects:**

```
📁 Resources/Audio/
├── pea_shoot       # Peashooter bắn đạn
├── plant_place     # Đặt cây xuống tile
├── plant_shovel    # Dùng shovel đào cây
├── sun_collect     # Thu thập sun
├── brain_collect   # Thu thập brain
├── zombie_groan/   # Folder chứa nhiều variations
├── frozen          # Zombie bị đóng băng
├── explosion       # Cherry Bomb/Doom-Shroom
├── game_win        # Thắng trận
└── game_lose       # Thua trận
```

---

## 3.4. QUẢN LÝ SCENE VÀ GAME STATE

### 3.4.1. Scene Management

**5 Scenes trong game:**

```
┌───────────────────────────────────────────────────────────────────────┐
│                           SCENE FLOW                                   │
├───────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  LoginScene ─────► LobbyScene ─────► LoadingScene ─────► GameScene    │
│       │                  │                  │                  │       │
│  - Unity Auth       - Select Role      - Setup Relay       - Selection│
│  - PlayFab Login    - Create/Join      - Connect Host      - Intro    │
│  - Enter Username   - Heartbeat        - Connect Client    - Countdown│
│                                                             - Playing  │
│                                                             - GameOver │
│                                                                        │
│                          TestScene (Development Only)                  │
│                          - Offline testing                             │
│                          - No networking                               │
└───────────────────────────────────────────────────────────────────────┘
```

### 3.4.2. Game State Machine

**File**: `Scripts/Networking/GameStateManager.cs` (281 dòng)

```csharp
public class GameStateManager : NetworkBehaviour
{
    public enum GameState
    {
        Waiting,    // Chờ đủ người chơi
        Selection,  // Chọn đội hình
        Intro,      // Camera intro
        Countdown,  // Đếm ngược
        Playing,    // Gameplay chính
        GameOver    // Kết thúc
    }

    // NetworkVariables for sync
    public NetworkVariable<GameState> CurrentState;
    public NetworkVariable<bool> IsPlantReady;
    public NetworkVariable<bool> IsZombieReady;
    private NetworkVariable<float> gameTimeRemaining;
    private NetworkVariable<PlayerRole> winner;

    // Events
    public Action<GameState> OnStateChanged;
    public Action<PlayerRole> OnGameEnded;
    public Action<float> OnTimeUpdated;
}
```

**State Transitions:**

```csharp
private void Update()
{
    if (!IsServer) return;

    switch (CurrentState.Value)
    {
        case GameState.Waiting:
            if (ConnectedClients >= 2)
                SetState(GameState.Selection);
            break;

        case GameState.Selection:
            // Wait for SetPlayerReadyServerRpc
            break;

        case GameState.Playing:
            UpdateGameTimer();
            if (gameTimeRemaining.Value <= 0)
                EndGame(PlayerRole.Plant);  // Time up = Plant wins
            break;
    }
}
```

### 3.4.3. NetworkVariables Sync

| Variable            | Type                          | Sync Mode    |
| ------------------- | ----------------------------- | ------------ |
| `CurrentState`      | `NetworkVariable<GameState>`  | Server → All |
| `IsPlantReady`      | `NetworkVariable<bool>`       | Server → All |
| `IsZombieReady`     | `NetworkVariable<bool>`       | Server → All |
| `gameTimeRemaining` | `NetworkVariable<float>`      | Server → All |
| `winner`            | `NetworkVariable<PlayerRole>` | Server → All |
| `currentHealth`     | `NetworkVariable<int>`        | Server → All |

---

## 3.5. CÁC KỸ THUẬT NÂNG CAO ĐÃ ÁP DỤNG

### 3.5.1. Singleton Pattern

**Áp dụng cho:** Tất cả Manager classes

```csharp
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

**Danh sách Singletons:**

- `LobbyManager.Instance`
- `NetworkGameManager.Instance`
- `GameStateManager.Instance`
- `PlantManager.Instance`
- `ZombieManager.Instance`
- `FusionManager.Instance`
- `SoundManager.Instance`
- `UIManager.Instance`

### 3.5.2. Object Pooling

**Áp dụng cho:** AudioSources, Projectiles (tương lai)

```csharp
// SoundManager.cs
private Queue<AudioSource> audioSourcePool;

private AudioSource GetAudioSource()
{
    if (audioSourcePool.Count == 0)
        return CreateNewAudioSource();

    AudioSource source = audioSourcePool.Dequeue();
    return source ?? CreateNewAudioSource();
}

private void ReturnToPool(AudioSource source, float delay)
{
    StartCoroutine(ReturnToPoolRoutine(source, delay));
}
```

### 3.5.3. Server-Authoritative Architecture

**Nguyên tắc:** Host là Server, xử lý tất cả game logic

```csharp
// ZombieBase.cs
public virtual void TakeDamage(int damage)
{
    if (!IsServer) return;  // CHỈ Server xử lý damage

    currentHealth.Value -= damage;
    if (currentHealth.Value <= 0)
        Die();
}

// PlantBase.cs
protected virtual void Die()
{
    if (!IsServer) return;  // CHỈ Server xử lý death

    NetworkObject netObj = GetComponent<NetworkObject>();
    netObj?.Despawn();
    Destroy(gameObject);
}
```

### 3.5.4. RPC Pattern (Remote Procedure Call)

**ServerRpc:** Client → Server

```csharp
[ServerRpc(RequireOwnership = false)]
public void SetPlayerReadyServerRpc(PlayerRole role, bool isReady,
    ServerRpcParams rpcParams = default)
{
    if (role == PlayerRole.Plant)
        IsPlantReady.Value = isReady;
    else if (role == PlayerRole.Zombie)
        IsZombieReady.Value = isReady;
}
```

**ClientRpc:** Server → All Clients

```csharp
[ClientRpc]
private void TriggerShootAnimationClientRpc()
{
    if (animator != null)
        animator.SetBool("isShooting", true);
}

[ClientRpc]
public void PlaySoundClientRpc(string soundName)
{
    SoundManager.Instance?.PlaySound(soundName);
}
```

### 3.5.5. Status Effect System

**Kỹ thuật:** Stack Multiplicative cho multiple slow sources

```csharp
// ZombieBase.cs
public void ApplySlow(float duration, float slowAmount, string sourceId,
    bool isFreezing = false, bool showFreezeVFX = false)
{
    // Tạo hoặc update slow effect
    SlowEffect effect = new SlowEffect
    {
        sourceId = sourceId,
        slowAmount = slowAmount,
        remainingDuration = duration,
        isFreezing = isFreezing
    };

    activeSlows[sourceId] = effect;
    RecalculateSlowMultiplier();
}

private void RecalculateSlowMultiplier()
{
    float multiplier = 1f;
    foreach (var effect in activeSlows.Values)
    {
        multiplier *= (1f - effect.slowAmount);  // Multiplicative stack
    }
    currentSlowMultiplier = multiplier;
}
```

### 3.5.6. Animation Events

**Kỹ thuật:** Gọi function từ Animation clip

```csharp
// Peashooter.cs
// Animation Event gọi SpawnPea() tại frame cụ thể
private void SpawnPea()
{
    if (!IsServer) return;
    StartCoroutine(ShootBurst());
}

// Animation Event gọi khi animation kết thúc
private void OnShootAnimationComplete()
{
    ResetShootingState();
}
```

### 3.5.7. Exponential Backoff

**Áp dụng cho:** Lobby heartbeat khi bị rate limited

```csharp
// LobbyManager.cs
private float minPollInterval = 10f;
private float maxPollInterval = 30f;
private int consecutiveErrors = 0;

private async void SendHeartbeat()
{
    try
    {
        await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        consecutiveErrors = 0;
    }
    catch (LobbyServiceException e)
    {
        consecutiveErrors++;
        float backoff = Mathf.Min(
            minPollInterval * Mathf.Pow(2, consecutiveErrors),
            maxPollInterval
        );
        await Task.Delay((int)(backoff * 1000));
    }
}
```

---

## 3.6. THỐNG KÊ MÃ NGUỒN

| Thư mục               | Số file | Tổng LOC (ước tính) |
| --------------------- | ------- | ------------------- |
| `Scripts/Networking/` | 12      | ~2,500              |
| `Scripts/Plants/`     | 11      | ~1,500              |
| `Scripts/Zombies/`    | 7       | ~1,200              |
| `Scripts/Utilities/`  | 22      | ~2,500              |
| `Scripts/UI/`         | 4       | ~600                |
| **Tổng**              | **56**  | **~8,300 LOC**      |

---

_Kết thúc Chương 3: Cài đặt và xây dựng_
