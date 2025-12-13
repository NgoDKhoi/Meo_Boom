# 💣 Meo Boom - Game Mèo Nổ (Exploding Kittens)

> **Meo Boom** là đồ án môn học **NT106.Q11**, mô phỏng lại tựa game thẻ bài nổi tiếng "Exploding Kittens" với tính năng chơi qua mạng (LAN/Online).

## 🏫 Thông Tin Đồ Án (Project Info)

* **Môn học:** Lập trình mạng căn bản (NT106.Q11)
* **Giảng viên hướng dẫn:** ThS. Trần Hồng Nghi
* **Nhóm thực hiện:** Nhóm 16
* **Trường:** Đại học Công nghệ Thông tin (UIT)

## 👥 Thành Viên Nhóm (Team Members)

| STT | MSSV | Họ và Tên | Vai Trò |
| :---: | :---: | :--- | :--- |
| 1 | 24520923 | `Huỳnh Cao Kỳ` | Leader |
| 2 | 24520860 | `Ngô Đình Khôi` | Member |
| 3 | 24520867 | `Phan Tuấn Khôi` | Member |
| 4 | 24520844 | `Trần Nguyễn Đăng Khoa` | Member|

## 🎮 Giới Thiệu Gameplay (How to Play)

Mục tiêu tối thượng: **ĐỪNG ĐỂ BỊ NỔ!** 💥

Trò chơi xoay quanh bộ bài chứa những lá "mèo nổ". Các người chơi sẽ lần lượt rút bài và tìm cách để trở thành người sống sót cuối cùng.
1. **Rút bài:** Khi đến lượt, bạn buộc phải rút một lá bài trên cùng của bộ bài để kết thúc lượt của bạn (trừ khi bạn dùng lá chức năng để né).
2. **Mèo Nổ:** Khi rút trúng lá **Mèo nổ**:
   * Có lá **Gỡ Bom (Defuse)**: Bạn sẽ đánh ra để gỡ bom và được đặt lại lá mèo nổ vào vị trí ngẫu nhiên trong chồng bài.
   * Không có lá **Gỡ Bom (Defuse)**: thì bạn sẽ **BOOM** và bị loại khỏi cuộc chơi (**GAME OVER!**) .
3. **Chiến thuật:** Sử dụng các lá bài chức năng trên tay để:
   * **Skip:** Kết thúc lượt mà không cần rút bài.
   * **Attack:** Ép người chơi kế tiếp rút 2 lá.
   * **See the Future:** Xem trước 3 lá bài đầu tiên trên bộ bài.
   * **Shuffle:** Xào lại bộ bài.
   * **Draw the bottom:** Rút lá dưới cuối của bộ bài.
- Ngoài ra thì còn 2 lá quan trọng là:
   * **Defuse**: Dùng để gỡ bom khi bốc trúng bom
   * **Explode**: Lá Bom
4. **Chiến thắng:** Người chơi cuối cùng còn sống sót là người chiến thắng.

## 🛠 Công Nghệ Sử Dụng (Tech Stack)

* **Game Engine:** Unity
* **Language:** C#
* **Authentication:** Firebase (Login, Logout, Forgot Password)
* **Networking:** Socket TCP/IP
* **Design Tools:** Krita

## 🌟 Tính Năng Nổi Bật (Key Features)

* **Hệ thống Tài khoản:** Tích hợp **Firebase Authentication** hỗ trợ Đăng nhập, Đăng xuất và Quên mật khẩu an toàn.
* **Single-player (PvE):** Chế độ Chơi với Máy giúp người chơi luyện tập kỹ năng. Bot được lập trình để tự động xử lý bài và tránh bom thông minh.
* **Multiplayer Real-time:** Hỗ trợ kết nối nhiều người chơi cùng lúc thông qua mạng LAN/Internet (Đang phát triển).
* **Room Management:** Tính năng tạo phòng, tìm phòng và sảnh chờ .
* **Chat System:** Hỗ trợ chat text trong sảnh chờ và trong ván chơi. (Đang phát triển)
* **Visual Effects:** Hiệu ứng nổ, âm thanh sống động và giao diện thân thiện.  

## 📸 Hình Ảnh Demo (Screenshots)
| Màn hình Đăng nhập (Login) | Giao diện Chơi (Gameplay) |
| :---: | :---: |
| <img src="Assets/Sprites/MeoNo_image/MenuLogin.png" width="400"> | <img src="Assets/Sprites/MeoNo_image/Gameplay.jpg" width="400"> |
| **Hiệu ứng Xào bài (Shuffle)** | **Hiệu ứng Nổ (Explosion)** |
| <img src="Assets/Sprites/MeoNo_image/HieuUngShuffle.jpg" width="400"> | <img src="Assets/Sprites/MeoNo_image/HieuUngNo.jpg" width="400"> |


## 🚀 Hướng Dẫn Cài Đặt (Installation)

1. Clone repository này về máy:
   ```bash
   git clone [https://github.com/username/Meo-Boom.git](https://github.com/username/Meo-Boom.git)
   ```
2. Mở project bằng Unity Hub (Version 2022.3.x trở lên).

3. Vào thư mục Scenes, mở LoginScene.

4. Nhấn Play để chạy game (Cần chạy Server trước nếu game dùng kiến trúc Client-Server tách biệt). 


**Cảm ơn Thầy và các bạn đã quan tâm đến dự án Meo Boom của Nhóm 16!**