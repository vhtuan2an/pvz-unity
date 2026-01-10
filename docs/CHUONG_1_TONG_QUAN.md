# CHƯƠNG 1: TỔNG QUAN VỀ ĐỒ ÁN

## 1. GIỚI THIỆU ĐỀ TÀI

### 1.1. Bối cảnh

Plants vs Zombies là một trong những tựa game Tower Defense nổi tiếng nhất mọi thời đại, được phát triển bởi PopCap Games. Game gốc là chế độ chơi đơn (single-player), người chơi đặt các loại cây để bảo vệ ngôi nhà khỏi đám zombie xâm lược.

### 1.2. Ý tưởng đổi mới

Đồ án này mang đến một trải nghiệm hoàn toàn mới: **Game đối kháng 1v1 Multiplayer Online** giữa hai người chơi thật. Thay vì đấu với AI, hai người chơi sẽ đối đầu trực tiếp:

- **Người chơi Plant**: Đặt cây để phòng thủ, ngăn chặn zombie
- **Người chơi Zombie**: Điều khiển và triệu hồi zombie để xâm chiếm sân

### 1.3. Điểm nổi bật

- ✅ **Multiplayer thời gian thực** qua Internet
- ✅ **Hệ thống Matchmaking** tự động ghép cặp người chơi
- ✅ **Hai gameplay hoàn toàn khác biệt** cho mỗi phe
- ✅ **Hệ thống Fusion** ghép cây tạo cây mạnh hơn
- ✅ **Hiệu ứng trạng thái** (Slow, Freeze) với VFX đẹp mắt

---

## 2. THÔNG TIN CƠ BẢN

| Mục               | Chi tiết                                                 |
| ----------------- | -------------------------------------------------------- |
| **Tên game**      | PvZ-Unity (Plants vs Zombies Unity Multiplayer)          |
| **Thể loại**      | Tower Defense / Real-Time Strategy (RTS) Multiplayer 1v1 |
| **Nền tảng**      | PC (Windows / macOS / Linux)                             |
| **Độ phân giải**  | 1920 x 1080 (Full HD)                                    |
| **Số người chơi** | 2 người chơi online                                      |

### 2.1. Công cụ và Công nghệ sử dụng

| Công nghệ                | Phiên bản / Chi tiết                   |
| ------------------------ | -------------------------------------- |
| **Unity Engine**         | Unity 6 (Universal 2D Template v5.1.0) |
| **Ngôn ngữ lập trình**   | C#                                     |
| **Render Pipeline**      | Universal Render Pipeline (URP) 2D     |
| **Networking Framework** | Unity Netcode for GameObjects          |
| **Relay Service**        | Unity Relay (kết nối P2P)              |
| **Lobby Service**        | Unity Lobby (quản lý phòng chơi)       |
| **Authentication**       | Unity Authentication + PlayFab SDK     |
| **UI Framework**         | Unity UI + TextMeshPro                 |
| **Animation**            | Unity Animator (Sprite Animation)      |

---

## 3. MỤC TIÊU ĐỒ ÁN

### 3.1. Mục tiêu tổng quan

Xây dựng một game multiplayer 1v1 hoàn chỉnh dựa trên gameplay của Plants vs Zombies, cho phép hai người chơi đối kháng trực tiếp qua Internet.

### 3.2. Mục tiêu cụ thể

#### 🎯 Về mặt kỹ thuật

1. **Networking**

   - Triển khai hệ thống multiplayer đồng bộ thời gian thực
   - Xử lý latency và đồng bộ game state giữa client-server
   - Sử dụng Relay để vượt qua NAT/Firewall

2. **Authentication & Security**

   - Tích hợp hệ thống đăng nhập an toàn
   - Quản lý session và player data

3. **Matchmaking**
   - Hệ thống tìm trận tự động
   - Tạo/tham gia phòng chơi (lobby)

#### 🎮 Về mặt gameplay

1. Thiết kế gameplay cân bằng giữa 2 phe
2. Hệ thống tài nguyên (Sun/Brain) riêng biệt
3. Đa dạng các loại cây và zombie với khả năng đặc biệt
4. Hệ thống Fusion (nâng cấp cây)

#### 🎨 Về mặt trải nghiệm người dùng

1. Giao diện trực quan, dễ sử dụng
2. Hiệu ứng hình ảnh và âm thanh hấp dẫn
3. Flow game mượt mà từ đăng nhập đến kết thúc trận

---

## 4. PHẠM VI ĐỒ ÁN

### 4.1. Số lượng màn chơi

| Loại           | Số lượng | Chi tiết                          |
| -------------- | -------- | --------------------------------- |
| **Scenes**     | 5        | Login, Lobby, Loading, Game, Test |
| **Maps**       | 1        | Sân cỏ cổ điển (5 lane x 9 cột)   |
| **Game Modes** | 1        | 1v1 Multiplayer                   |

### 4.2. Số lượng Units

| Phe         | Số lượng prefabs | Chi tiết                                                                                                                                                                                         |
| ----------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Plants**  | 16+              | Peashooter, Repeater, Threepeater, Gatling Pea, Mega Gatling Pea, Sunflower, Twin Sunflower, Wallnut, Cherry Bomb, Doom-Shroom, Potato Mine, Bonk Choy, Kernel-pult, Snow Pea, Winter-mint, v.v. |
| **Zombies** | 10+              | Basic, Allstar, Kamehameha, Mixi, Cannon, Con Trai, Mummy, Pirate, Bungee, Mummified Gargantuar, v.v.                                                                                            |

### 4.3. Các tính năng chính

#### ✅ Tính năng đã hoàn thành

| #   | Tính năng                 | Mô tả                                |
| --- | ------------------------- | ------------------------------------ |
| 1   | **Authentication System** | Đăng nhập Unity Auth + PlayFab       |
| 2   | **Lobby System**          | Tạo, tìm, join phòng chơi            |
| 3   | **Matchmaking**           | Tự động ghép cặp người chơi          |
| 4   | **Role Selection**        | Chọn phe Plant hoặc Zombie           |
| 5   | **Unit Selection UI**     | Chọn đội hình 7 units trước trận     |
| 6   | **Ready System**          | Xác nhận sẵn sàng của cả 2 người     |
| 7   | **Game State Machine**    | Quản lý các phase của game           |
| 8   | **Sun Economy**           | Thu thập, tiêu sun để trồng cây      |
| 9   | **Brain Economy**         | Thu thập brain để triệu hồi zombie   |
| 10  | **Plant Placement**       | Đặt cây lên grid tile                |
| 11  | **Zombie Spawning**       | Triệu hồi zombie vào lane            |
| 12  | **Combat System**         | Plants tấn công zombies và ngược lại |
| 13  | **Cooldown System**       | Thời gian chờ sau khi dùng unit      |
| 14  | **Shovel Tool**           | Đào và loại bỏ cây đã đặt            |
| 15  | **Fusion System**         | Ghép 2 cây thành cây mạnh hơn        |
| 16  | **Status Effects**        | Slow, Freeze với VFX                 |
| 17  | **Win/Lose Conditions**   | Zombie đến đích = Zombie thắng       |
| 18  | **Sound System**          | Âm thanh và hiệu ứng                 |
| 19  | **Network Sync**          | Đồng bộ state giữa 2 clients         |
| 20  | **Preview System**        | Xem trước vị trí đặt cây             |

## 5. CẤU TRÚC THƯ MỤC

```
📁 pvz-unity/
├── 📁 Assets/
│   ├── 📁 Animations/          # Animation clips và controllers (260 files)
│   ├── 📁 Prefabs/             # Prefabs game objects
│   │   ├── 📁 Plants/          # 16+ plant prefabs + FusionRecipe
│   │   ├── 📁 Zombies/         # 10+ zombie prefabs
│   │   ├── 📁 Managers/        # Manager prefabs
│   │   ├── 📁 UI/              # UI prefabs
│   │   └── 📁 Utilities/       # Utility prefabs
│   ├── 📁 Scenes/              # 5 Unity scenes
│   ├── 📁 Scripts/             # C# source code
│   │   ├── 📁 Camera/          # Camera controllers
│   │   ├── 📁 Networking/      # Network, lobby, auth (12 files)
│   │   ├── 📁 Plants/          # Plant behaviors (11 files)
│   │   ├── 📁 Players/         # Player controllers
│   │   ├── 📁 UI/              # UI scripts (4 files)
│   │   ├── 📁 Utilities/       # Managers, projectiles (42+ files)
│   │   │   ├── 📁 Plant/       # PlantManager, SeedPacket, Sun...
│   │   │   └── 📁 Zombie/      # ZombieManager, Brain...
│   │   └── 📁 Zombies/         # Zombie behaviors (7 files)
│   ├── 📁 Sprites/             # Artwork (10,000+ sprite files)
│   │   ├── 📁 Plant Sprites/   # Plant animations (2,885 files)
│   │   ├── 📁 Zombie Sprites/  # Zombie animations (7,263 files)
│   │   ├── 📁 UI/              # UI elements
│   │   └── 📁 lawn/            # Background
│   ├── 📁 Resources/           # Runtime loadable assets
│   ├── 📁 PlayFabSDK/          # PlayFab integration
│   └── 📁 TextMesh Pro/        # TextMeshPro assets
├── 📁 Packages/                # Unity package dependencies
├── 📁 ProjectSettings/         # Unity project settings
└── 📄 pvz-unity.slnx           # Solution file
```
