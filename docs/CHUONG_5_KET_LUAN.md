# CHƯƠNG 5: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN

## 5.1. KẾT LUẬN

### 5.1.1. Tổng kết dự án

Đồ án **PvZ-Unity** đã hoàn thành mục tiêu xây dựng một game multiplayer 1v1 hoàn chỉnh dựa trên gameplay của Plants vs Zombies. Dự án đã thành công trong việc biến đổi một tựa game Tower Defense single-player truyền thống thành trải nghiệm đối kháng PvP cạnh tranh giữa hai người chơi thật.

### 5.1.2. Các thành tựu chính

#### ✅ Về mặt kỹ thuật

1. **Triển khai Multiplayer thành công**

   - Sử dụng Unity Netcode for GameObjects cho networking
   - Tích hợp Unity Relay để vượt qua NAT/Firewall
   - Đồng bộ realtime ổn định giữa 2 clients

2. **Kiến trúc hệ thống rõ ràng**

   - Client-Server model với Host làm authoritative server
   - Game State Machine quản lý 6 trạng thái game
   - Modular code với các Singleton managers

3. **Tích hợp dịch vụ cloud**
   - Unity Authentication cho xác thực người dùng
   - Unity Lobby cho quản lý phòng chơi
   - PlayFab SDK cho lưu trữ dữ liệu

#### ✅ Về mặt gameplay

1. **Gameplay cân bằng và đa dạng**

   - 16 loại Plants với chức năng riêng biệt
   - 10 loại Zombies với khả năng đặc biệt
   - Hệ thống Fusion tạo chiều sâu chiến thuật

2. **Cơ chế game hoàn chỉnh**

   - Hệ thống tài nguyên Sun/Brain độc lập
   - Combat system đa dạng (Projectile, Melee, AOE)
   - Status effects (Slow, Freeze) với VFX

3. **Flow game mượt mà**
   - 5 scenes với chuyển cảnh liền mạch
   - Điều kiện thắng/thua rõ ràng
   - Timer 5 phút tạo áp lực chiến thuật

#### ✅ Về mặt trải nghiệm người dùng

1. **Giao diện trực quan**

   - HUD riêng biệt cho từng phe
   - Preview vị trí đặt cây
   - Cooldown indicator rõ ràng

2. **Hiệu ứng đẹp mắt**
   - 260+ animation clips cho nhân vật
   - VFX cho status effects
   - 10,000+ sprites chất lượng cao

### 5.1.3. Những khó khăn đã vượt qua

| Khó khăn                            | Giải pháp                                |
| ----------------------------------- | ---------------------------------------- |
| **Đồng bộ trạng thái qua mạng**     | Sử dụng NetworkVariables và RPC calls    |
| **Xử lý latency**                   | Host authoritative với client prediction |
| **Quản lý phức tạp game state**     | State Machine pattern với 6 states       |
| **Cân bằng gameplay 2 phe**         | Thiết kế economy và units riêng biệt     |
| **Rate limiting từ Unity Services** | Exponential backoff cho Lobby heartbeat  |

### 5.1.4. Bài học kinh nghiệm

1. **Lập kế hoạch kiến trúc từ đầu**: Việc thiết kế Singleton managers và State Machine từ đầu giúp code dễ maintain và mở rộng.

2. **Test networking sớm**: Multiplayer bugs khó debug, cần test từ giai đoạn đầu phát triển.

3. **Modular design**: Tách biệt logic Plants/Zombies qua base class giúp thêm nhân vật mới nhanh chóng.

4. **Sử dụng cloud services**: Unity Gaming Services giảm đáng kể công sức phát triển backend.

---

## 5.2. HƯỚNG PHÁT TRIỂN TRONG TƯƠNG LAI

### 5.2.1. Cải tiến ngắn hạn (1-3 tháng)

| Ưu tiên | Tính năng        | Mô tả                                     |
| ------- | ---------------- | ----------------------------------------- |
| 🔴 Cao  | **Thêm map mới** | Pool, Night, Roof maps từ game gốc        |
| 🔴 Cao  | **Balancing**    | Điều chỉnh stats dựa trên player feedback |
| 🟡 TB   | **Thêm Plants**  | Chomper, Cactus, Squash, Jalapeno, v.v.   |
| 🟡 TB   | **Thêm Zombies** | Football, Newspaper, Dolphin Rider, v.v.  |
| 🟢 Thấp | **UI polish**    | Animations, transitions đẹp hơn           |

### 5.2.2. Tính năng trung hạn (3-6 tháng)

| Tính năng              | Mô tả                             |
| ---------------------- | --------------------------------- |
| **Ranked Mode**        | Hệ thống xếp hạng với ELO rating  |
| **Leaderboard**        | Bảng xếp hạng top players         |
| **Replay System**      | Xem lại trận đấu                  |
| **Spectator Mode**     | Chế độ xem trận đấu               |
| **Achievement System** | Hệ thống thành tựu và phần thưởng |
| **Daily Challenges**   | Nhiệm vụ hàng ngày                |

### 5.2.3. Tính năng dài hạn (6+ tháng)

| Tính năng                   | Mô tả                                       |
| --------------------------- | ------------------------------------------- |
| **2v2 Mode**                | Hỗ trợ 4 người chơi (2 Plants vs 2 Zombies) |
| **Tournament Mode**         | Chế độ giải đấu                             |
| **Custom Game Mode Editor** | Cho phép người chơi tạo rules riêng         |
| **Mobile Port**             | Phát hành trên iOS/Android                  |
| **Cross-platform Play**     | Chơi chéo giữa PC và Mobile                 |
| **Workshop/Modding**        | Cho phép cộng đồng tạo content              |

### 5.2.4. Cải tiến kỹ thuật

| Lĩnh vực        | Cải tiến đề xuất                            |
| --------------- | ------------------------------------------- |
| **Performance** | Object pooling cho projectiles và effects   |
| **Networking**  | Dedicated server thay vì host-client model  |
| **Security**    | Server-side validation chống cheat          |
| **Analytics**   | Tích hợp Unity Analytics cho game telemetry |
| **CI/CD**       | Automated build và deployment pipeline      |

### 5.2.5. Roadmap đề xuất

```
Q1 2026                   Q2 2026                   Q3 2026                   Q4 2026
┌─────────────────────────┬─────────────────────────┬─────────────────────────┬─────────────────────────┐
│ • Thêm 5 Plants mới     │ • Ranked Mode           │ • 2v2 Mode              │ • Mobile Beta           │
│ • Thêm 5 Zombies mới    │ • Leaderboard           │ • Tournament            │ • Cross-platform        │
│ • 2 Maps mới            │ • Replay System         │ • Achievements          │ • Workshop support      │
│ • Balance patch         │ • Spectator Mode        │ • Daily Challenges      │ • Official Launch       │
└─────────────────────────┴─────────────────────────┴─────────────────────────┴─────────────────────────┘
```

---

## 5.3. LỜI KẾT

Dự án PvZ-Unity là một bước tiến quan trọng trong việc học hỏi và áp dụng các công nghệ game development hiện đại. Từ networking multiplayer, game state management đến integration với cloud services, dự án đã cover nhiều khía cạnh quan trọng của việc phát triển game.

Mặc dù còn nhiều tính năng có thể được bổ sung trong tương lai, phiên bản hiện tại đã đạt được tất cả các mục tiêu đề ra ban đầu. Game hoạt động ổn định, gameplay vui nhộn và mang đến trải nghiệm mới mẻ cho người chơi yêu thích tựa game Plants vs Zombies huyền thoại.

---

## 👥 THÔNG TIN NHÓM

| Thành viên | Vai trò |
| ---------- | ------- |
| ...        | ...     |

---

## 📝 GHI CHÚ

- Game sử dụng sprites và assets từ Plants vs Zombies gốc cho mục đích học tập
- Đây là đồ án phi thương mại
- Tất cả bản quyền sprites thuộc về PopCap Games / EA

---

_Tài liệu được tạo từ phân tích source code - Cập nhật: 2026-01-08_

_Kết thúc Chương 5: Kết luận và hướng phát triển_
