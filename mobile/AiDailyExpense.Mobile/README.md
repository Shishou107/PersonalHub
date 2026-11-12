# AI Daily Expense Mobile Client

Ứng dụng di động **AI Daily Expense** được phát triển trên nền tảng **React Native** sử dụng bộ công cụ **Expo** và lập trình bằng **TypeScript**.

---

## 1. Công nghệ chính
- React Native & Expo SDK 52+
- TypeScript
- React Navigation (Tabs, Native Stack)
- Axios Client (Request/Response Interceptors)
- AsyncStorage (Lưu trữ JWT token cục bộ)

---

## 2. Hướng dẫn chạy ứng dụng

### Yêu cầu
- Đã cài đặt **Node.js** phiên bản v18 trở lên.
- Đã cài đặt ứng dụng **Expo Go** trên thiết bị di động (nếu muốn test trên điện thoại thật).

### Cấu hình Endpoint kết nối API
Mở file `src/utils/constants.ts` và thiết lập biến `LOCAL_API_URL` để khớp với địa chỉ máy chủ API Backend:
- Nếu chạy trên Android Emulator: API mặc định trỏ về `http://10.0.2.2:5000/api`.
- Nếu chạy trên iOS Simulator / Web: API mặc định trỏ về `http://localhost:5000/api`.
- Nếu chạy trên điện thoại thật: Thay đổi địa chỉ IP của máy tính của bạn trong mạng LAN nội bộ (ví dụ: `http://192.168.1.10:5000/api`).

### Các bước chạy
1. Truy cập thư mục Mobile:
   ```bash
   cd mobile/AiDailyExpense.Mobile
   ```
2. Cài đặt các thư viện phụ thuộc:
   ```bash
   npm install
   ```
3. Khởi động Expo Server:
   ```bash
   npx expo start
   ```
4. Quét mã QR bằng Expo Go trên Android/iOS để mở ứng dụng, hoặc nhấn phím `a` trên Terminal để chạy Android Emulator.

---

## 3. Tài khoản thử nghiệm có sẵn
- **Email**: `test@example.com`
- **Mật khẩu**: `123456`
(Lưu ý: Bạn cần đăng ký tài khoản trước hoặc chạy Backend API trước để cơ sở dữ liệu gieo tài khoản này tự động).
