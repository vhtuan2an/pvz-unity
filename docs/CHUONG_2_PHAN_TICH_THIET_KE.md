# CHƯƠNG 2: PHÂN TÍCH VÀ THIẾT KẾ GAME

## 2.1. Ý TƯỞNG GAME VÀ LUẬT CHƠI

### 2.1.1. Ý tưởng cốt lõi

**Concept:** Biến đổi game Tower Defense đơn người chơi thành game **đối kháng PvP (Player vs Player) 1v1** thời gian thực qua Internet.

**Điểm độc đáo:**

- Game gốc Plants vs Zombies là single-player, người chơi luôn đóng vai phe Plants
- Trong phiên bản này, có **2 người chơi thật**, mỗi người điều khiển một phe
- Tạo ra trải nghiệm cạnh tranh và chiến thuật sâu sắc hơn

### 2.1.2. Luật chơi chi tiết

#### 📋 Luật cơ bản

| Quy tắc             | Mô tả                                                  |
| ------------------- | ------------------------------------------------------ |
| **Số người chơi**   | 2 người (1 Plant, 1 Zombie)                            |
| **Mục tiêu Plant**  | Ngăn chặn zombie trong thời gian quy định              |
| **Mục tiêu Zombie** | Đưa ít nhất 1 zombie đến cuối sân (bên trái)           |
| **Thời gian trận**  | 5 phút (300 giây)                                      |
| **Điều kiện thắng** | Zombie chạm đích = Zombie thắng, Hết giờ = Plant thắng |

#### 🌻 Luật phe Plants

```
┌─────────────────────────────────────────────────────────────┐
│                    GAMEPLAY PHE PLANTS                       │
├─────────────────────────────────────────────────────────────┤
│  1. Thu thập SUN từ trời rơi xuống hoặc từ Sunflower        │
│  2. Chọn cây từ deck (tối đa 7 loại đã chọn trước)          │
│  3. Click vào ô trống trên sân để đặt cây                   │
│  4. Mỗi cây có chi phí SUN và thời gian cooldown riêng      │
│  5. Có thể dùng Shovel để đào bỏ cây đã đặt                 │
│  6. Ghép cây (Fusion) để tạo cây mạnh hơn                   │
└─────────────────────────────────────────────────────────────┘
```

**Hệ thống Sun (Mặt trời):**

- **Sun từ trời**: Rơi ngẫu nhiên mỗi 10 giây
- **Sun từ Sunflower**: Sản xuất mỗi 24 giây
- **Click để thu thập**: Sun biến mất sau vài giây nếu không thu
- **Giá trị**: 25 Sun/mỗi sun (cả từ trời và Sunflower)

#### 🧟 Luật phe Zombies

```
┌─────────────────────────────────────────────────────────────┐
│                    GAMEPLAY PHE ZOMBIES                      │
├─────────────────────────────────────────────────────────────┤
│  1. Thu thập BRAIN từ trời rơi xuống                         │
│  2. Chọn zombie từ deck (tối đa 7 loại đã chọn trước)       │
│  3. Click vào lane bên phải để triệu hồi zombie             │
│  4. Mỗi zombie có chi phí BRAIN và cooldown riêng           │
│  5. Zombie tự động đi sang trái và tấn công cây             │
│  6. Đưa 1 zombie đến cuối sân để chiến thắng                │
└─────────────────────────────────────────────────────────────┘
```

**Hệ thống Brain (Não):**

- **Brain từ trời**: Rơi ngẫu nhiên mỗi 10 giây
- **Click để thu thập**: Brain biến mất sau vài giây
- **Giá trị**: 25 Brain/mỗi brain

### 2.1.3. Flow trận đấu

```
         ┌───────────────┐
         │   MATCHMAKING │  ← Hai người chơi được ghép cặp
         └───────┬───────┘
                 ▼
         ┌───────────────┐
         │   SELECTION   │  ← Mỗi người chọn 7 units cho deck
         └───────┬───────┘
                 ▼
         ┌───────────────┐
         │     READY     │  ← Xác nhận sẵn sàng
         └───────┬───────┘
                 ▼
         ┌───────────────┐
         │   COUNTDOWN   │  ← "Ready... Set... Plant!"
         └───────┬───────┘
                 ▼
    ┌────────────────────────┐
    │        PLAYING         │  ← Gameplay chính (5 phút)
    │                        │
    │  Plant: Đặt cây        │
    │  Zombie: Spawn zombie  │
    │                        │
    └───────────┬────────────┘
                │
        ┌───────┴───────┐
        ▼               ▼
┌───────────────┐ ┌───────────────┐
│ ZOMBIE THẮNG  │ │  PLANT THẮNG  │
│ (Chạm đích)   │ │ (Hết giờ)     │
└───────────────┘ └───────────────┘
```

---

## 2.2. THIẾT KẾ HỆ THỐNG GAME

### 2.2.1. Game State Machine (Máy trạng thái)

```csharp
// Trích từ GameStateManager.cs
public enum GameState
{
    Waiting,    // Chờ đủ người chơi
    Selection,  // Chọn đội hình
    Intro,      // Camera intro
    Countdown,  // Đếm ngược
    Playing,    // Gameplay
    GameOver    // Kết thúc
}
```

**Sơ đồ trạng thái:**

```
Waiting ──[2 players connected]──→ Selection
                                      │
                          [Both ready]│
                                      ▼
                                    Intro
                                      │
                        [Intro done]  │
                                      ▼
                                  Countdown
                                      │
                          [3-2-1 GO!] │
                                      ▼
                                   Playing
                                    │   │
       [Zombie wins] ───────────────┘   └──────────── [Time up]
                │                                        │
                ▼                                        ▼
           GameOver                                  GameOver
        (Zombie wins)                             (Plant wins)
```

### 2.2.2. Hệ thống Grid và Tile

**Cấu trúc sân chơi:**

```
       Cột 1   Cột 2   Cột 3   Cột 4   Cột 5   Cột 6   Cột 7   Cột 8   Cột 9
      ┌───────┬───────┬───────┬───────┬───────┬───────┬───────┬───────┬───────┐
Lane 1│ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ ← Zombie spawn
      ├───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
Lane 2│ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │
      ├───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
Lane 3│ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │
      ├───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
Lane 4│ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │
      ├───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
Lane 5│ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │ Tile  │
      └───────┴───────┴───────┴───────┴───────┴───────┴───────┴───────┴───────┘
  ↑                                                                            ↑
Win Zone                                                                 Zombie Spawn
(Bên trái)                                                               (Bên phải)
```

**Logic Tile (trích `Tile.cs`):**

| Property             | Mô tả                        |
| -------------------- | ---------------------------- |
| `IsOccupied`         | Tile đã có cây hay chưa      |
| `Occupant`           | Reference đến cây đang chiếm |
| `PlantWorldPosition` | Vị trí world để đặt cây      |
| `TryOccupy(plant)`   | Thử đặt cây vào tile         |
| `Clear()`            | Xóa cây khỏi tile            |

### 2.2.3. Hệ thống Combat (Chiến đấu)

**Luồng chiến đấu:**

```
                    ┌─────────────────┐
                    │   PLANT ATTACK  │
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        ▼                    ▼                    ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  Projectile   │   │    Melee      │   │     AOE       │
│ (Peashooter)  │   │  (BonkChoy)   │   │ (CherryBomb)  │
└───────┬───────┘   └───────┬───────┘   └───────┬───────┘
        │                   │                   │
        └───────────────────┼───────────────────┘
                            ▼
                  ┌─────────────────┐
                  │ Zombie.TakeDamage│
                  └────────┬────────┘
                           │
              ┌────────────┼────────────┐
              ▼            ▼            ▼
       ┌──────────┐ ┌──────────┐ ┌──────────┐
       │  Alive   │ │  Slowed  │ │   Dead   │
       └──────────┘ └──────────┘ └──────────┘
```

**Bảng sát thương (trích từ source code):**

| Attacker     | Damage | Attack Rate | Range    | Type       |
| ------------ | ------ | ----------- | -------- | ---------- |
| Peashooter   | 20     | 1.5s        | 12 units | Projectile |
| Repeater     | 20 x 2 | 1.5s        | 12 units | Burst      |
| Gatling Pea  | 20 x 4 | 1.5s        | 12 units | Burst      |
| Cherry Bomb  | 1800   | Instant     | 3.5x3.5  | AOE        |
| Bonk Choy    | 15     | 0.3s        | Melee    | Melee      |
| Basic Zombie | 1      | 1s          | Melee    | Melee      |

### 2.2.4. Hệ thống Fusion (Ghép cây)

**Nguyên lý:**

- Đặt cùng loại cây (hoặc cây tương thích) lên cây đã có trên tile
- Cây gốc bị phá hủy, cây mới (upgrade) được spawn

**Sơ đồ Fusion chuỗi Peashooter:**

```
Level 1          Level 2          Level 3          Level 4           Level 5
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌─────────────┐    ┌────────────────┐
│Peashooter│ +  │Peashooter│ =  │ Repeater │ +  │  Repeater   │ =  │  Threepeater   │
│(1 đạn)   │    │          │    │ (2 đạn)  │    │             │    │  (3 lane)      │
└──────────┘    └──────────┘    └────┬─────┘    └─────────────┘    └───────┬────────┘
                                     │                                      │
                                     │       ┌──────────────┐               │
                                     │       │ Threepeater  │               │
                                     │       └──────┬───────┘               │
                                     │              │                       │
                                     ▼              ▼                       ▼
                              ┌─────────────────────────────────────────────────┐
                              │               GATLING PEA                        │
                              │               (4 đạn/shot)                       │
                              └───────────────────────┬─────────────────────────┘
                                                      │
                                                      ▼
                              ┌─────────────────────────────────────────────────┐
                              │            MEGA GATLING PEA                      │
                              │            (Maximum firepower)                   │
                              └─────────────────────────────────────────────────┘
```

**Fusion đặc biệt - Wallnut First Aid:**

```csharp
// Trích từ FusionManager.cs
if (existingPlant.name.Contains("Wallnut") && plantToPlace.name.Contains("Wallnut"))
{
    Wallnut wallnut = existingPlant.GetComponent<Wallnut>();
    if (wallnut.CurrentHealth < wallnut.MaxHealth)
    {
        wallnut.RestoreHealth();  // Hồi full HP thay vì fusion
        return true;
    }
}
```

### 2.2.5. Hệ thống Status Effects

**Slow Effect (Làm chậm):**

```csharp
// Trích từ ZombieBase.cs
public void ApplySlow(float duration, float slowAmount, string sourceId)
{
    // slowAmount: 0.0 - 1.0 (0% - 100% slow)
    // Stack multiplicatively
    currentSlowMultiplier *= (1f - slowAmount);
}
```

| Source               | Slow Amount | Duration | Visual                 |
| -------------------- | ----------- | -------- | ---------------------- |
| Snow Pea             | 30%         | 2s       | Blue tint              |
| Kernel-pult (Butter) | 100%        | 3s       | Ice VFX                |
| Winter-mint          | 100%        | 5s       | Ice VFX + Frozen sound |

**Freeze Effect (Đóng băng):**

- 100% slow = Dừng hoàn toàn
- Animation speed = 0
- Spawn Ice VFX tại chân hoặc đầu zombie
- Play sound "frozen"

---

## 2.3. THIẾT KẾ NHÂN VẬT VÀ MÀN CHƠI

### 2.3.1. Thiết kế Plants (Cây)

#### Phân loại theo chức năng:

```
                            ┌─────────────┐
                            │   PLANTS    │
                            └──────┬──────┘
                                   │
       ┌───────────────┬───────────┼───────────┬───────────────┐
       ▼               ▼           ▼           ▼               ▼
┌─────────────┐ ┌─────────────┐ ┌─────────┐ ┌─────────────┐ ┌─────────────┐
│   OFFENSE   │ │   DEFENSE   │ │ECONOMY  │ │   SUPPORT   │ │    BOMB     │
│  (Tấn công) │ │  (Phòng thủ)│ │(Kinh tế)│ │  (Hỗ trợ)   │ │ (Bom/Trap)  │
├─────────────┤ ├─────────────┤ ├─────────┤ ├─────────────┤ ├─────────────┤
│ Peashooter  │ │   Wallnut   │ │Sunflower│ │ Winter-mint │ │ Cherry Bomb │
│ Repeater    │ │             │ │Twin Sun │ │ Kernel-pult │ │ Doom-Shroom │
│ Threepeater │ │             │ │         │ │ (Butter)    │ │ Potato Mine │
│ Gatling Pea │ │             │ │         │ │             │ │             │
│ Snow Pea    │ │             │ │         │ │             │ │             │
│ Bonk Choy   │ │             │ │         │ │             │ │             │
└─────────────┘ └─────────────┘ └─────────┘ └─────────────┘ └─────────────┘
```

#### Chi tiết từng plant:

**1. Peashooter (Đậu bắn đạn)**

```
┌────────────────────────────────────────────────────────────┐
│ PEASHOOTER                                                  │
├────────────────────────────────────────────────────────────┤
│ Chi phí: 100 Sun      │ Cooldown: 7.5s                     │
│ HP: 100               │ Attack Rate: 1.5s                  │
├────────────────────────────────────────────────────────────┤
│ BEHAVIOR:                                                   │
│ - Phát hiện zombie trong lane (12 units phía trước)        │
│ - Trigger animation "isShooting"                           │
│ - Spawn PeaProjectile tại shootPoint                       │
│ - Đạn bay thẳng, damage 20 khi va chạm zombie              │
├────────────────────────────────────────────────────────────┤
│ UPGRADES: → Repeater → Threepeater → Gatling Pea          │
└────────────────────────────────────────────────────────────┘
```

**2. Sunflower (Hoa hướng dương)**

```
┌────────────────────────────────────────────────────────────┐
│ SUNFLOWER                                                   │
├────────────────────────────────────────────────────────────┤
│ Chi phí: 50 Sun       │ Cooldown: 7.5s                     │
│ HP: 100               │ Production: 24s/sun               │
├────────────────────────────────────────────────────────────┤
│ BEHAVIOR:                                                   │
│ - Mỗi 24 giây trigger "isProducing" animation              │
│ - Spawn Sun với bounce animation:                          │
│   1. Bounce lên 0.5 units (ease out)                       │
│   2. Drop xuống 0.3 units (ease in)                        │
│ - Sun có thể được click thu thập                           │
│ - Random blink animation mỗi 5-15 giây                     │
├────────────────────────────────────────────────────────────┤
│ UPGRADES: → Twin Sunflower (2 sun cùng lúc)                │
└────────────────────────────────────────────────────────────┘
```

**3. Wallnut (Quả óc chó)**

```
┌────────────────────────────────────────────────────────────┐
│ WALLNUT                                                     │
├────────────────────────────────────────────────────────────┤
│ Chi phí: 50 Sun       │ Cooldown: 30s                      │
│ HP: 4000              │ Type: Tank/Defense                │
├────────────────────────────────────────────────────────────┤
│ BEHAVIOR:                                                   │
│ - Không tấn công, chỉ chịu sát thương                      │
│ - Animation thay đổi theo HP:                              │
│   > 75%: "Idle" (nguyên vẹn)                               │
│   > 50%: "Degrade1" (hơi nứt)                              │
│   > 25%: "Degrade2" (nứt nhiều)                            │
│   ≤ 25%: "Degrade3" (sắp vỡ)                               │
│ - Có thể "First Aid": Đặt Wallnut lên Wallnut bị thương    │
│   để restore full HP                                        │
└────────────────────────────────────────────────────────────┘
```

**4. Cherry Bomb (Bom Cherry)**

```
┌────────────────────────────────────────────────────────────┐
│ CHERRY BOMB                                                 │
├────────────────────────────────────────────────────────────┤
│ Chi phí: 150 Sun      │ Cooldown: 50s                      │
│ HP: N/A (Instant use) │ Damage: 1800 (kills most zombies) │
├────────────────────────────────────────────────────────────┤
│ BEHAVIOR:                                                   │
│ - Khi đặt xuống, chờ animation hoàn tất                    │
│ - Explode() gọi OverlapBoxAll với size 3.5 x 3.5           │
│ - Gây 1800 damage cho tất cả zombies trong vùng            │
│ - Tự hủy sau animation                                      │
├────────────────────────────────────────────────────────────┤
│ USE CASE: Emergency clear khi bị overwhelm                  │
└────────────────────────────────────────────────────────────┘
```

### 2.3.2. Thiết kế Zombies

#### Phân loại theo chức năng:

```
                            ┌─────────────┐
                            │   ZOMBIES   │
                            └──────┬──────┘
                                   │
       ┌───────────────┬───────────┼───────────┬───────────────┐
       ▼               ▼           ▼           ▼               ▼
┌─────────────┐ ┌─────────────┐ ┌─────────┐ ┌─────────────┐ ┌─────────────┐
│    BASIC    │ │   RUSHER    │ │ RANGED  │ │   SPECIAL   │ │    BOSS     │
│  (Cơ bản)   │ │  (Xông pha) │ │(Tầm xa) │ │  (Đặc biệt) │ │   (Boss)    │
├─────────────┤ ├─────────────┤ ├─────────┤ ├─────────────┤ ├─────────────┤
│Basic Zombie │ │   Allstar   │ │ Cannon  │ │ MixiZombie  │ │ Gargantuar  │
│             │ │   Zombie    │ │ K.meha  │ │   ConTrai   │ │             │
└─────────────┘ └─────────────┘ └─────────┘ └─────────────┘ └─────────────┘
```

#### Chi tiết từng zombie:

**1. Basic Zombie**

```
┌────────────────────────────────────────────────────────────┐
│ BASIC ZOMBIE                                                │
├────────────────────────────────────────────────────────────┤
│ Chi phí: 50 Brain     │ Cooldown: 7.5s                     │
│ HP: 10                │ Move Speed: 1 unit/s               │
│ Damage: 1/hit         │ Attack Rate: 1s                   │
├────────────────────────────────────────────────────────────┤
│ BEHAVIOR:                                                   │
│ - Sau startDelay (0.5s), bắt đầu đi                        │
│ - Kiểm tra BoxCast về phía trước (0.01 units)              │
│ - Nếu không gặp Plant: Đi tiếp (isWalking = true)          │
│ - Nếu gặp Plant: Dừng và ăn (isEating = true)              │
│ - Gây damage mỗi attackRate giây                           │
│ - Khi chết: Trigger "Die" animation, đợi 1s rồi despawn    │
└────────────────────────────────────────────────────────────┘
```

**2. Allstar Zombie (Cầu thủ)**

```
┌────────────────────────────────────────────────────────────┐
│ ALLSTAR ZOMBIE                                              │
├────────────────────────────────────────────────────────────┤
│ Chi phí: 150 Brain    │ Cooldown: 15s                      │
│ HP: 30                │ Move Speed: 2.5 unit/s (Rush)      │
│ Damage: 5/hit         │ Attack Rate: 0.8s                  │
├────────────────────────────────────────────────────────────┤
│ BEHAVIOR:                                                   │
│ - Di chuyển nhanh hơn zombie thường                        │
│ - Có khả năng "tackle" (lao về phía trước)                 │
│ - Tank hơn với HP cao                                       │
├────────────────────────────────────────────────────────────┤
│ USE CASE: Xuyên thủng hàng phòng thủ mỏng                  │
└────────────────────────────────────────────────────────────┘
```

### 2.3.3. Thiết kế màn chơi

**Lawn (Sân cỏ chính):**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              GAME SCENE LAYOUT                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────┐                                                        ┌────────┐ │
│  │ SUN  │  ┌─────────────────────────────────────────────────┐  │ BRAIN  │ │
│  │ 100  │  │                                                 │  │  50    │ │
│  └──────┘  │                    LAWN                         │  └────────┘ │
│            │              (5 lanes x 9 columns)              │             │
│  ┌──────┐  │                                                 │  ┌────────┐ │
│  │Packet│  │  Lane 1: ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │  │Packet  │ │
│  │  1   │  │  Lane 2: ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │  │   1    │ │
│  ├──────┤  │  Lane 3: ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │  ├────────┤ │
│  │Packet│  │  Lane 4: ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │  │Packet  │ │
│  │  2   │  │  Lane 5: ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │  │   2    │ │
│  ├──────┤  │                                                 │  ├────────┤ │
│  │  ...│  └─────────────────────────────────────────────────┘  │  ...   │ │
│  └──────┘                                                       └────────┘ │
│  [SHOVEL]                      ⏱️ 4:32                         [ZOMBIE]    │
│                                                                WIN ZONE    │
└─────────────────────────────────────────────────────────────────────────────┘
     ↑                                                                ↑
Plant Player HUD                                            Zombie Player HUD
```

---

## 2.4. THIẾT KẾ GIAO DIỆN NGƯỜI DÙNG (UI)

### 2.4.1. Login Scene UI

```
┌─────────────────────────────────────────────────────────────┐
│                                                              │
│                     🌻 PLANTS vs ZOMBIES 🧟                  │
│                        MULTIPLAYER                           │
│                                                              │
│              ┌────────────────────────────────┐              │
│              │      Enter Username            │              │
│              │  ┌──────────────────────────┐  │              │
│              │  │ [________________]       │  │              │
│              │  └──────────────────────────┘  │              │
│              │                                │              │
│              │      ┌─────────────────┐       │              │
│              │      │     LOGIN       │       │              │
│              │      └─────────────────┘       │              │
│              └────────────────────────────────┘              │
│                                                              │
│                     Status: Connecting...                    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 2.4.2. Lobby Scene UI

```
┌─────────────────────────────────────────────────────────────┐
│  Welcome, Player123!                              [LOGOUT]  │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│    ┌─────────────────┐    ┌─────────────────┐               │
│    │                 │    │                 │               │
│    │   🌻 PLANTS     │    │   🧟 ZOMBIES    │               │
│    │                 │    │                 │               │
│    │  [  SELECT  ]   │    │  [  SELECT  ]   │               │
│    └─────────────────┘    └─────────────────┘               │
│                                                              │
├─────────────────────────────────────────────────────────────┤
│                      AVAILABLE LOBBIES                       │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ Host Name     │ Role    │ Players │ [JOIN]             ││
│  ├───────────────┼─────────┼─────────┼────────────────────┤│
│  │ Player456     │ Plants  │   1/2   │ [JOIN AS ZOMBIE]   ││
│  │ Player789     │ Zombies │   1/2   │ [JOIN AS PLANTS]   ││
│  └─────────────────────────────────────────────────────────┘│
│                                                              │
│              ┌──────────────────────────────┐                │
│              │      CREATE NEW LOBBY         │                │
│              └──────────────────────────────┘                │
└─────────────────────────────────────────────────────────────┘
```

### 2.4.3. Selection UI

```
┌─────────────────────────────────────────────────────────────┐
│                    CHOOSE YOUR DECK (7 max)                  │
├───────────────────────────────────┬─────────────────────────┤
│                                   │    SELECTED (7/7)       │
│  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ │   ┌─────┐               │
│  │🌻50 │ │🌻125│ │🌿100│ │🌿100│ │   │🌻50 │ Sunflower     │
│  └─────┘ └─────┘ └─────┘ └─────┘ │   ├─────┤               │
│  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ │   │🌿100│ Peashooter    │
│  │🥜50 │ │🍒150│ │🥔25 │ │🥊150│ │   ├─────┤               │
│  └─────┘ └─────┘ └─────┘ └─────┘ │   │🥜50 │ Wallnut       │
│  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ │   ├─────┤               │
│  │🌽100│ │❄️100│ │☠️125│ │...  │ │   │🍒150│ Cherry Bomb   │
│  └─────┘ └─────┘ └─────┘ └─────┘ │   ├─────┤               │
│                                   │   │...  │               │
│          [Available Units]        │   └─────┘               │
│                                   │                         │
├───────────────────────────────────┴─────────────────────────┤
│  🌻 Plant Player: [ READY ]     🧟 Zombie: Waiting...       │
│                                                              │
│                    ┌─────────────────┐                       │
│                    │      READY      │                       │
│                    └─────────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

### 2.4.4. Gameplay HUD

**Plant Player View:**

```
┌─────────────────────────────────────────────────────────────┐
│ ☀️ 175                                            ⏱️ 4:32   │
├──────────┬──────────────────────────────────────────────────┤
│ ┌──────┐ │                                                  │
│ │🌻 50 │ │                                                  │
│ ├──────┤ │                                                  │
│ │🌿 100│ │              [GAME AREA]                         │
│ ├──────┤ │                                                  │
│ │🥜 50 │ │                                                  │
│ ├──────┤ │                                                  │
│ │🍒 150│ │                                                  │
│ ├──────┤ │                                                  │
│ │...   │ │                                                  │
│ └──────┘ │                                                  │
│ [SHOVEL] │                                                  │
└──────────┴──────────────────────────────────────────────────┘
```

**Zombie Player View:**

```
┌─────────────────────────────────────────────────────────────┐
│ ⏱️ 4:32                                            🧠 125   │
├──────────────────────────────────────────────────┬──────────┤
│                                                  │ ┌──────┐ │
│                                                  │ │🧟 50 │ │
│                                                  │ ├──────┤ │
│              [GAME AREA]                         │ │⚽ 150│ │
│                                                  │ ├──────┤ │
│                                                  │ │💀 200│ │
│                                                  │ ├──────┤ │
│                                                  │ │...   │ │
│                                                  │ └──────┘ │
└──────────────────────────────────────────────────┴──────────┘
```

---

## 2.5. CÁC THIẾT KẾ KHÁC

### 2.5.1. Networking Design

**Client-Server Model:**

```
┌─────────────────────────────────────────────────────────────┐
│                    NETWORKING ARCHITECTURE                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   Player 1 (Host)              Player 2 (Client)            │
│   ┌───────────────┐            ┌───────────────┐            │
│   │   Local       │            │    Local      │            │
│   │   Input       │            │    Input      │            │
│   └───────┬───────┘            └───────┬───────┘            │
│           │                            │                     │
│           ▼                            ▼                     │
│   ┌───────────────┐            ┌───────────────┐            │
│   │   ServerRpc   │◄───────────│   ServerRpc   │            │
│   │   (Host)      │   Relay    │               │            │
│   └───────┬───────┘            └───────────────┘            │
│           │                                                  │
│           ▼                                                  │
│   ┌───────────────────────────────────────────┐             │
│   │         AUTHORITATIVE SERVER (Host)        │             │
│   │  - Validate actions                        │             │
│   │  - Spawn/Despawn NetworkObjects            │             │
│   │  - Update NetworkVariables                 │             │
│   │  - Check win conditions                    │             │
│   └───────────────────┬───────────────────────┘             │
│                       │                                      │
│                       ▼                                      │
│   ┌───────────────┐            ┌───────────────┐            │
│   │   ClientRpc   │────────────│   ClientRpc   │            │
│   │   (Host)      │   Relay    │   (Client)    │            │
│   └───────────────┘            └───────────────┘            │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

**NetworkVariables sử dụng:**

| Variable                 | Type                         | Mô tả             |
| ------------------------ | ---------------------------- | ----------------- |
| `CurrentState`           | `NetworkVariable<GameState>` | Trạng thái game   |
| `IsPlantReady`           | `NetworkVariable<bool>`      | Plant đã ready?   |
| `IsZombieReady`          | `NetworkVariable<bool>`      | Zombie đã ready?  |
| `gameTimeRemaining`      | `NetworkVariable<float>`     | Thời gian còn lại |
| `currentHealth` (Zombie) | `NetworkVariable<int>`       | HP zombie (sync)  |

### 2.5.2. Resource System Design

**Sun Generation Flow:**

```
┌──────────────┐
│  SunSpawner  │
│  (Server)    │
└──────┬───────┘
       │ initialDelay: 1s
       │ spawnInterval: 10s
       ▼
┌──────────────────────────┐
│  SpawnSunFromSky()       │
│  - Random X position     │
│  - Fall with gravity     │
│  - Stop after 0.8-1.8s   │
└──────────────┬───────────┘
               │
               ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│        Sun.cs            │    │     Sunflower.cs         │
│  - Click to collect      │    │  - Every 24s             │
│  - +25 Sun               │    │  - Spawn sun with bounce │
│  - Auto-despawn 8s       │    │  - +25 Sun when clicked  │
└──────────────────────────┘    └──────────────────────────┘
               │                           │
               └───────────────────────────┘
                           │
                           ▼
               ┌──────────────────────────┐
               │     PlantManager         │
               │  currentSun += 25        │
               │  UpdateSunCounter()      │
               └──────────────────────────┘
```

### 2.5.3. Animation System

**Animator State Machine (Ví dụ Peashooter):**

```
                    ┌───────────┐
                    │   Idle    │◄──────────────────────┐
                    └─────┬─────┘                       │
                          │                             │
              [isShooting = true]                       │
                          │                      [animation ends]
                          ▼                             │
                    ┌───────────┐                       │
                    │  Shoot    │───────────────────────┘
                    └───────────┘
                          │
                    [Animation Event]
                          │
                          ▼
                    SpawnPea()
```

**Animator State Machine (Ví dụ Wallnut):**

```
                    ┌───────────┐
        ┌──────────│   Idle    │──────────┐
        │          │  (>75%)   │          │
        │          └───────────┘          │
        │                                 │
[HP > 75%]                          [HP ≤ 75%]
        │                                 │
        │          ┌───────────┐          │
        └──────────│ Degrade1  │◄─────────┘
                   │  (>50%)   │
                   └─────┬─────┘
                         │
                   [HP ≤ 50%]
                         │
                         ▼
                   ┌───────────┐
                   │ Degrade2  │
                   │  (>25%)   │
                   └─────┬─────┘
                         │
                   [HP ≤ 25%]
                         │
                         ▼
                   ┌───────────┐
                   │ Degrade3  │
                   │  (≤25%)   │
                   └───────────┘
```

### 2.5.4. Projectile System

**Pea Projectile Lifecycle:**

```
1. SPAWN
   ├─ Instantiate at shootPoint
   ├─ NetworkObject.Spawn()
   └─ Set initial velocity (Vector2.right)

2. TRAVEL
   ├─ Move rightward
   ├─ Check collision with "Zombie" layer
   └─ Check out of bounds

3. HIT
   ├─ OnTriggerEnter2D with Zombie
   ├─ zombie.TakeDamage(damage)
   ├─ Play hit VFX (optional)
   └─ Destroy projectile

4. DESTROY
   ├─ NetworkObject.Despawn()
   └─ Destroy(gameObject)
```

### 2.5.5. Sound Design

**Danh sách Sound Effects:**

| Sound Name      | Trigger Event           |
| --------------- | ----------------------- |
| `pea_shoot`     | Peashooter bắn đạn      |
| `plant_place`   | Đặt cây xuống tile      |
| `plant_shovel`  | Dùng shovel đào cây     |
| `sun_collect`   | Thu thập sun            |
| `brain_collect` | Thu thập brain          |
| `zombie_groan`  | Zombie spawn            |
| `zombie_eat`    | Zombie ăn cây           |
| `frozen`        | Zombie bị đóng băng     |
| `explosion`     | Cherry Bomb/Doom-Shroom |
| `game_win`      | Thắng trận              |
| `game_lose`     | Thua trận               |
